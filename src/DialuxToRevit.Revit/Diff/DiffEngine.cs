using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using DialuxToRevit.Core.Model;
using DialuxToRevit.Revit.Placement;
using DialuxToRevit.Revit.Storage;

namespace DialuxToRevit.Revit.Diff
{
    /// <summary>
    /// Works out what a re-import would change, without changing anything.
    ///
    /// The whole point is to preserve element identity. Deleting and recreating
    /// a luminaire that merely shifted loses its ElementId, and with it the
    /// circuit, the tags and the schedule rows attached to it -- so a fixture
    /// that moved is moved, and a fixture whose product changed has its type
    /// swapped in place.
    /// </summary>
    public static class DiffEngine
    {
        public static ImportDiff Compute(Document document, DialuxImportResult import,
            PlacementOptions options, DiffOptions diffOptions)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (import == null)
            {
                throw new ArgumentNullException(nameof(import));
            }

            options = options ?? new PlacementOptions();
            diffOptions = diffOptions ?? new DiffOptions();

            ImportDiff diff = new ImportDiff { SourceFile = options.SourceFile };

            List<ExistingLuminaire> existing =
                ExistingLuminaireIndex.Collect(document, options.SourceFile);

            List<Candidate> incoming = CollectIncoming(import, options, diff);

            if (diffOptions.ReplaceAll)
            {
                // Everything goes and everything comes back. Offered for when an
                // export has changed so completely that matching it up would be
                // guesswork, at the cost of every circuit and tag on the old
                // fixtures.
                foreach (ExistingLuminaire element in existing)
                {
                    diff.Entries.Add(new DiffEntry
                    {
                        Action = DiffAction.Delete,
                        ExistingId = element.Id,
                        Caution = element.Caution
                    });
                }

                foreach (Candidate candidate in incoming)
                {
                    diff.Entries.Add(new DiffEntry
                    {
                        Action = DiffAction.Add,
                        Incoming = candidate.Instance,
                        Group = candidate.Group
                    });
                }

                return diff;
            }

            Match(diff, existing, incoming, options, diffOptions);
            return diff;
        }

        private sealed class Candidate
        {
            public LuminaireInstance Instance;
            public PlacementGroup Group;
            public string Key;
            public bool Taken;
        }

        /// <summary>
        /// The luminaires the export wants placed. Groups with no family chosen
        /// are left out of the comparison entirely rather than being reported as
        /// deletions -- the user did not ask for them either way.
        /// </summary>
        private static List<Candidate> CollectIncoming(DialuxImportResult import,
            PlacementOptions options, ImportDiff diff)
        {
            List<Candidate> candidates = new List<Candidate>();

            foreach (PlacementGroup group in import.Groups)
            {
                string mappingKey = FamilyMapping.MakeKey(group.ProductBlock, group.ZMillimetres);
                if (!options.Mappings.TryGetValue(mappingKey, out FamilyMapping mapping)
                    || !mapping.IsMapped)
                {
                    diff.SkippedGroups.Add(string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} type {1} @ {2:F0} mm ({3} luminaires)",
                        group.Storey, group.TypeIndex, group.ZMillimetres, group.Count));
                    continue;
                }

                foreach (LuminaireInstance instance in group.Instances)
                {
                    candidates.Add(new Candidate
                    {
                        Instance = instance,
                        Group = group,
                        Key = DialuxStamp.MakeKey(
                            instance.ProductBlock, instance.X, instance.Y, instance.Z)
                    });
                }
            }

            return candidates;
        }

        private static void Match(ImportDiff diff, List<ExistingLuminaire> existing,
            List<Candidate> incoming, PlacementOptions options, DiffOptions diffOptions)
        {
            HashSet<ElementId> claimed = new HashSet<ElementId>();

            // Pass 1: an identical key is the same luminaire, untouched.
            Dictionary<string, ExistingLuminaire> byKey =
                new Dictionary<string, ExistingLuminaire>(StringComparer.Ordinal);

            foreach (ExistingLuminaire element in existing)
            {
                if (!string.IsNullOrEmpty(element.Key) && !byKey.ContainsKey(element.Key))
                {
                    byKey[element.Key] = element;
                }
            }

            foreach (Candidate candidate in incoming)
            {
                if (byKey.TryGetValue(candidate.Key, out ExistingLuminaire match)
                    && !claimed.Contains(match.Id))
                {
                    claimed.Add(match.Id);
                    candidate.Taken = true;

                    diff.Entries.Add(new DiffEntry
                    {
                        Action = DiffAction.Unchanged,
                        Incoming = candidate.Instance,
                        Group = candidate.Group,
                        ExistingId = match.Id
                    });
                }
            }

            List<ExistingLuminaire> unclaimed =
                existing.Where(e => !claimed.Contains(e.Id)).ToList();

            // Pass 2: same spot, different product -- the luminaire was swapped,
            // so change its type rather than replacing the element.
            foreach (Candidate candidate in incoming.Where(c => !c.Taken))
            {
                ExistingLuminaire match = NearestWithin(
                    unclaimed, candidate, options, diffOptions.SamePositionToleranceMillimetres,
                    requireSameProduct: false, out double distance);

                if (match == null || SameProduct(match, candidate))
                {
                    continue;
                }

                claimed.Add(match.Id);
                unclaimed.Remove(match);
                candidate.Taken = true;

                diff.Entries.Add(new DiffEntry
                {
                    Action = DiffAction.Retype,
                    Incoming = candidate.Instance,
                    Group = candidate.Group,
                    ExistingId = match.Id,
                    DistanceMillimetres = distance,
                    Caution = match.Caution
                });
            }

            // Pass 3: same product nearby -- it shifted. Moving keeps the
            // ElementId, and with it everything attached to the fixture.
            foreach (Candidate candidate in incoming.Where(c => !c.Taken))
            {
                ExistingLuminaire match = NearestWithin(
                    unclaimed, candidate, options, diffOptions.MoveThresholdMillimetres,
                    requireSameProduct: true, out double distance);

                if (match == null)
                {
                    continue;
                }

                claimed.Add(match.Id);
                unclaimed.Remove(match);
                candidate.Taken = true;

                diff.Entries.Add(new DiffEntry
                {
                    Action = DiffAction.Move,
                    Incoming = candidate.Instance,
                    Group = candidate.Group,
                    ExistingId = match.Id,
                    DistanceMillimetres = distance
                });
            }

            // Whatever is left over is genuinely new or genuinely gone.
            foreach (Candidate candidate in incoming.Where(c => !c.Taken))
            {
                diff.Entries.Add(new DiffEntry
                {
                    Action = DiffAction.Add,
                    Incoming = candidate.Instance,
                    Group = candidate.Group
                });
            }

            foreach (ExistingLuminaire element in unclaimed)
            {
                diff.Entries.Add(new DiffEntry
                {
                    Action = DiffAction.Delete,
                    ExistingId = element.Id,
                    Caution = element.Caution
                });
            }
        }

        private static bool SameProduct(ExistingLuminaire element, Candidate candidate)
        {
            return string.Equals(
                element.BlockId, candidate.Instance.ProductBlock, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Closest unclaimed element within a radius. Greedy nearest-first: with
        /// luminaires on a regular grid any smarter assignment would cost more
        /// than it is worth, and a wrong pairing between two identical fixtures
        /// a metre apart produces the same model either way.
        /// </summary>
        private static ExistingLuminaire NearestWithin(List<ExistingLuminaire> pool,
            Candidate candidate, PlacementOptions options, double radiusMillimetres,
            bool requireSameProduct, out double distance)
        {
            ExistingLuminaire best = null;
            double bestDistance = double.MaxValue;

            foreach (ExistingLuminaire element in pool)
            {
                if (requireSameProduct && !SameProduct(element, candidate))
                {
                    continue;
                }

                double d = ExistingLuminaireIndex.DistanceMillimetres(
                    element,
                    candidate.Instance.X,
                    candidate.Instance.Y,
                    candidate.Instance.Z,
                    options.Transform);

                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = element;
                }
            }

            if (best == null || bestDistance > radiusMillimetres)
            {
                distance = 0.0;
                return null;
            }

            distance = bestDistance;
            return best;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using DialuxToRevit.Core.Model;
using DialuxToRevit.Revit.Placement;

namespace DialuxToRevit.Revit.Diff
{
    /// <summary>Carries out a diff the user has seen and accepted.</summary>
    public sealed class DiffApplier
    {
        private readonly Document _document;
        private readonly LuminairePlacer _placer;

        public DiffApplier(Document document, LuminairePlacer placer)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _placer = placer ?? throw new ArgumentNullException(nameof(placer));
        }

        /// <summary>
        /// Applies every entry in one transaction.
        ///
        /// A luminaire Revit refuses is recorded and the rest still go through;
        /// an unexpected error rolls the whole thing back, because a half
        /// applied diff leaves a model nobody can tell from a finished one.
        /// </summary>
        public PlacementResult Apply(ImportDiff diff, PlacementOptions options, DiffOptions diffOptions)
        {
            if (diff == null)
            {
                throw new ArgumentNullException(nameof(diff));
            }

            options = options ?? new PlacementOptions();
            diffOptions = diffOptions ?? new DiffOptions();

            PlacementResult result = new PlacementResult
            {
                BatchId = options.BatchId,
                SourceFile = options.SourceFile,
                StartedAt = DateTime.Now
            };

            foreach (string skipped in diff.SkippedGroups)
            {
                result.Skipped.Add(skipped + ": no family chosen.");
            }

            using (Transaction transaction = new Transaction(_document, "Update DIALux luminaires"))
            {
                transaction.Start();

                try
                {
                    // Deletions first: a luminaire being replaced should not
                    // briefly coexist with the one taking its place.
                    ApplyDeletions(diff, diffOptions, result);
                    ApplyChanges(diff, options, result);
                }
                catch (Exception)
                {
                    transaction.RollBack();
                    throw;
                }

                transaction.Commit();
            }

            return result;
        }

        private void ApplyDeletions(ImportDiff diff, DiffOptions diffOptions, PlacementResult result)
        {
            List<ElementId> toDelete = new List<ElementId>();

            foreach (DiffEntry entry in diff.Entries.Where(e => e.Action == DiffAction.Delete))
            {
                if (entry.HasCaution && !diffOptions.DeleteCircuited)
                {
                    result.Skipped.Add(string.Format(
                        CultureInfo.CurrentCulture,
                        "Kept element {0}: {1}.", entry.ExistingId, entry.Caution));
                    continue;
                }

                toDelete.Add(entry.ExistingId);
            }

            if (toDelete.Count == 0)
            {
                return;
            }

            try
            {
                ICollection<ElementId> deleted = _document.Delete(toDelete);
                result.DeletedCount += deleted?.Count ?? toDelete.Count;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException exception)
            {
                result.Failures.Add("Could not delete: " + exception.Message);
            }
        }

        private void ApplyChanges(ImportDiff diff, PlacementOptions options, PlacementResult result)
        {
            // Resolving once per group rather than per luminaire keeps the
            // symbol activation and the level lookup out of the inner loop.
            Dictionary<PlacementGroup, ResolvedTarget> targets =
                new Dictionary<PlacementGroup, ResolvedTarget>();

            foreach (DiffEntry entry in diff.Entries)
            {
                if (entry.Action == DiffAction.Unchanged || entry.Action == DiffAction.Delete)
                {
                    if (entry.Action == DiffAction.Unchanged)
                    {
                        result.UnchangedCount++;
                    }

                    continue;
                }

                ResolvedTarget target = ResolveOnce(targets, entry.Group, options, result);
                if (target == null)
                {
                    continue;
                }

                try
                {
                    ApplyOne(entry, target, options, result);
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException exception)
                {
                    result.Failures.Add(string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} at ({1:F0}, {2:F0}): {3}",
                        entry.Action, entry.Incoming?.X ?? 0.0, entry.Incoming?.Y ?? 0.0,
                        exception.Message));
                }
            }
        }

        private void ApplyOne(DiffEntry entry, ResolvedTarget target,
            PlacementOptions options, PlacementResult result)
        {
            switch (entry.Action)
            {
                case DiffAction.Add:
                    _placer.EnsureActive(target.Entry.Symbol);
                    FamilyInstance created = _placer.Create(entry.Incoming, target, options);
                    result.PlacedIds.Add(created.Id);
                    Count(result, entry.Group);
                    break;

                case DiffAction.Move:
                    if (_document.GetElement(entry.ExistingId) is FamilyInstance moving)
                    {
                        _placer.MoveTo(moving, entry.Incoming, target, options);
                        result.MovedCount++;
                    }

                    break;

                case DiffAction.Retype:
                    if (_document.GetElement(entry.ExistingId) is FamilyInstance retyping)
                    {
                        _placer.Retype(retyping, entry.Incoming, target, options);
                        result.RetypedCount++;
                    }

                    break;
            }
        }

        private ResolvedTarget ResolveOnce(Dictionary<PlacementGroup, ResolvedTarget> cache,
            PlacementGroup group, PlacementOptions options, PlacementResult result)
        {
            if (group == null)
            {
                return null;
            }

            if (cache.TryGetValue(group, out ResolvedTarget cached))
            {
                return cached.IsUsable ? cached : null;
            }

            ResolvedTarget target = _placer.Resolve(group, options);
            cache[group] = target;

            if (!target.IsUsable)
            {
                result.Skipped.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} type {1} @ {2:F0} mm: {3}.",
                    group.Storey, group.TypeIndex, group.ZMillimetres, target.Problem));
                return null;
            }

            return target;
        }

        private static void Count(PlacementResult result, PlacementGroup group)
        {
            if (group == null)
            {
                return;
            }

            string label = string.Format(
                CultureInfo.CurrentCulture,
                "{0} type {1} @ {2:F0} mm",
                group.Storey, group.TypeIndex, group.ZMillimetres);

            result.PerGroup.TryGetValue(label, out int count);
            result.PerGroup[label] = count + 1;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using EntityFrameworkCore.Triggered.Internal.CascadeStrategies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EntityFrameworkCore.Triggered.Internal
{
    public sealed class TriggerContextTracker
    {
        readonly ChangeTracker _changeTracker;
        readonly ICascadeStrategy _cascadingStrategy;

        List<TriggerContextDescriptor>? _discoveredChanges;
        List<int>? _capturedChangeIndexes;

        public TriggerContextTracker(ChangeTracker changeTracker, ICascadeStrategy cascadingStrategy)
        {
            _changeTracker = changeTracker;
            _cascadingStrategy = cascadingStrategy;
        }

        static ChangeType? ResolveChangeType(EntityEntry entry) => entry.State switch {
            EntityState.Added => ChangeType.Added,
            EntityState.Modified => ChangeType.Modified,
            EntityState.Deleted => ChangeType.Deleted,
            _ => null,
        };

        public IEnumerable<TriggerContextDescriptor>? DiscoveredChanges
        {
            get
            {
                if (_discoveredChanges == null)
                {
                    return null;
                }
                else
                {
                    if (_capturedChangeIndexes == null)
                    {
                        return _discoveredChanges;
                    }
                    else
                    {
                        return _discoveredChanges.Where((_, index) => !_capturedChangeIndexes.Contains(index));
                    }
                }
            }
        }

        public IEnumerable<TriggerContextDescriptor> DiscoverChanges()
        {
            int startIndex;

            _changeTracker.DetectChanges();
            var entries = _changeTracker.Entries();

            if (_discoveredChanges == null)
            {
                _discoveredChanges = new List<TriggerContextDescriptor>(entries.Count());
                startIndex = 0;
            }
            else
            {
                startIndex = _discoveredChanges.Count;
            }

            foreach (var entry in entries)
            {
                var changeType = ResolveChangeType(entry);
                if (changeType != null)
                {
                    if (startIndex > 0)
                    {
                        var canCascade = true;

                        foreach (var discoveredChange in _discoveredChanges)
                        {
                            if (discoveredChange.Entity == entry.Entity)
                            {
                                canCascade = _cascadingStrategy.CanCascade(entry, changeType.Value, discoveredChange);

                                if (!canCascade)
                                {
                                    break;
                                }
                            }
                        }

                        if (!canCascade)
                        {
                            continue;
                        }
                    }

                    var triggerContextDescriptor = new TriggerContextDescriptor(entry, changeType.Value);

                    _discoveredChanges.Add(triggerContextDescriptor!);
                }
            }

            if (startIndex == 0)
            {
                return _discoveredChanges;
            }
            else
            {
                return _discoveredChanges.Skip(startIndex);
            }
        }

        public void CaptureChanges()
        {
            if (_discoveredChanges != null)
            {
                var changesCount = _discoveredChanges.Count;
                if (changesCount > 0)
                {
                    if (_capturedChangeIndexes == null)
                    {
                        _capturedChangeIndexes = new List<int>(changesCount); // assuming all will be captured
                    }

                    for (var changeIndex = 0; changeIndex < changesCount; changeIndex++)
                    {
                        if (!_capturedChangeIndexes.Contains(changeIndex))
                        {
                            var discoveredChange = _discoveredChanges[changeIndex];

                            var changeType = ResolveChangeType(discoveredChange.EntityEntry);

                            if (changeType != discoveredChange.ChangeType)
                            {
                                _capturedChangeIndexes.Add(changeIndex);
                            }
                        }
                    }
                }
            }
        }

        public void UncaptureChanges()
            => _capturedChangeIndexes = null;
    }
}

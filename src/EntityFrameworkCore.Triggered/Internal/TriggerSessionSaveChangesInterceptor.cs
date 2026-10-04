using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.Triggered.Internal
{
#pragma warning disable CS0618 // Type or member is obsolete (TriggeredDbContext with EFCore5)
    public class TriggerSessionSaveChangesInterceptor : ISaveChangesInterceptor, IResettableService
    {
#if DEBUG
        DbContext? _capturedDbContext;
#endif

        ITriggerSession? _triggerSession;
        int _parallelSaveChangesCount;
        int _afterSaveFailedTriggersRaisedDepth;
        bool _subscribedToSaveChangesFailed;

        // Triggers can call SaveChanges on the same context, so this tracks the nesting depth whose AfterSaveFailed triggers already ran
        private bool AfterSaveFailedTriggersRaised => _parallelSaveChangesCount > 0 && _afterSaveFailedTriggersRaisedDepth == _parallelSaveChangesCount;

        private void EnlistTriggerSession(DbContextEventData eventData)
        {
#if DEBUG
            if (_triggerSession != null)
            {
                Debug.Assert(_capturedDbContext == eventData.Context);
            }
            else
            {
                _capturedDbContext = eventData.Context;
            }
#endif

            if (_triggerSession == null)
            {
                if (eventData.Context is null)
                {
                    throw new InvalidOperationException("Expected a context");
                }

                var triggerService = eventData.Context.GetService<ITriggerService>() ?? throw new InvalidOperationException("Triggers are not configured");

                if (triggerService.Current != null)
                {
                    _triggerSession = triggerService.Current;
                }
                else
                {
                    _triggerSession = triggerService.CreateSession(eventData.Context);
                }
            }

            if (!_subscribedToSaveChangesFailed)
            {
                eventData.Context!.SaveChangesFailed += OnSaveChangesFailed;
                _subscribedToSaveChangesFailed = true;
            }

            _parallelSaveChangesCount += 1;
        }

        private void DelistTriggerSession(DbContext? context)
        {
            Debug.Assert(_triggerSession != null);

#if DEBUG
            Debug.Assert(_capturedDbContext == context);
#endif

            if (AfterSaveFailedTriggersRaised)
            {
                _afterSaveFailedTriggersRaisedDepth = 0;
            }

            _parallelSaveChangesCount -= 1;

            if (_parallelSaveChangesCount == 0)
            {
                _triggerSession.Dispose();
                _triggerSession = null;
            }
        }


        public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            EnlistTriggerSession(eventData);
            Debug.Assert(_triggerSession != null);

            var defaultAutoDetectChangesEnabled = eventData.Context!.ChangeTracker.AutoDetectChangesEnabled;

            try
            {
                eventData.Context.ChangeTracker.AutoDetectChangesEnabled = false;

                _triggerSession.RaiseBeforeSaveStartingTriggers();
                _triggerSession.RaiseBeforeSaveTriggers();
                _triggerSession.CaptureDiscoveredChanges();
                _triggerSession.RaiseBeforeSaveCompletedTriggers();
            }
            catch
            {
                // We're aborting the SaveChanges call, delist the trigger session now
                DelistTriggerSession(eventData.Context);
                throw;
            }
            finally
            {
                eventData.Context.ChangeTracker.AutoDetectChangesEnabled = defaultAutoDetectChangesEnabled;
            }

            return result;
        }

        public async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            EnlistTriggerSession(eventData);
            Debug.Assert(_triggerSession != null);

            var defaultAutoDetectChangesEnabled = eventData.Context!.ChangeTracker.AutoDetectChangesEnabled;

            try
            {
                eventData.Context.ChangeTracker.AutoDetectChangesEnabled = false;

                _triggerSession.RaiseBeforeSaveStartingTriggers();
                await _triggerSession.RaiseBeforeSaveStartingAsyncTriggers(cancellationToken).ConfigureAwait(false);

                _triggerSession.RaiseBeforeSaveTriggers();
                await _triggerSession.RaiseBeforeSaveAsyncTriggers(cancellationToken).ConfigureAwait(false);

                _triggerSession.CaptureDiscoveredChanges();

                _triggerSession.RaiseBeforeSaveCompletedTriggers();
                await _triggerSession.RaiseBeforeSaveCompletedAsyncTriggers(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // We're aborting the SaveChanges call, delist the trigger session now
                DelistTriggerSession(eventData.Context);
                throw;
            }
            finally
            {
                eventData.Context.ChangeTracker.AutoDetectChangesEnabled = defaultAutoDetectChangesEnabled;
            }

            return result;
        }

        public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            Debug.Assert(_triggerSession != null);

            _triggerSession.RaiseAfterSaveStartingTriggers();
            _triggerSession.RaiseAfterSaveTriggers();
            _triggerSession.RaiseAfterSaveCompletedTriggers();

            DelistTriggerSession(eventData.Context);

            return result;
        }

        public async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Debug.Assert(_triggerSession != null);

            _triggerSession.RaiseAfterSaveStartingTriggers();
            await _triggerSession.RaiseAfterSaveStartingAsyncTriggers(cancellationToken).ConfigureAwait(false);

            _triggerSession.RaiseAfterSaveTriggers();
            await _triggerSession.RaiseAfterSaveAsyncTriggers(cancellationToken).ConfigureAwait(false);

            _triggerSession.RaiseAfterSaveCompletedTriggers();
            await _triggerSession.RaiseAfterSaveCompletedAsyncTriggers(cancellationToken).ConfigureAwait(false);

            DelistTriggerSession(eventData.Context);
            
            return result;
        }

        public void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            try
            {
                if (!AfterSaveFailedTriggersRaised)
                {
                    RaiseAfterSaveFailedTriggers(eventData.Exception);
                }
            }
            finally
            {
                DelistTriggerSession(eventData.Context);
            }
        }

        public async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!AfterSaveFailedTriggersRaised)
                {
                    await RaiseAfterSaveFailedAsyncTriggers(eventData.Exception, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                DelistTriggerSession(eventData.Context);
            }
        }

        // An interceptor registered after us can still suppress the exception, so the session is only released once SaveChanges
        // reports its final outcome: SavedChanges, SaveChangesFailed, SaveChangesCanceled or the DbContext.SaveChangesFailed event.
        public InterceptionResult ThrowingConcurrencyException(ConcurrencyExceptionEventData eventData, InterceptionResult result)
        {
            if (!result.IsSuppressed && !AfterSaveFailedTriggersRaised)
            {
                try
                {
                    RaiseAfterSaveFailedTriggers(eventData.Exception);
                }
                finally
                {
                    _afterSaveFailedTriggersRaisedDepth = _parallelSaveChangesCount;
                }
            }

            return result;
        }

        public async ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(ConcurrencyExceptionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (!result.IsSuppressed && !AfterSaveFailedTriggersRaised)
            {
                try
                {
                    await RaiseAfterSaveFailedAsyncTriggers(eventData.Exception, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _afterSaveFailedTriggersRaisedDepth = _parallelSaveChangesCount;
                }
            }

            return result;
        }

        private void OnSaveChangesFailed(object? sender, SaveChangesFailedEventArgs eventArgs)
        {
            if (eventArgs.Exception is not DbUpdateConcurrencyException || _triggerSession is null)
            {
                return;
            }

            try
            {
                if (!AfterSaveFailedTriggersRaised)
                {
                    RaiseAfterSaveFailedTriggers(eventArgs.Exception);
                }
            }
            finally
            {
                DelistTriggerSession(sender as DbContext);
            }
        }

        public void SaveChangesCanceled(DbContextEventData eventData)
            => DelistTriggerSession(eventData.Context);

        public Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
        {
            DelistTriggerSession(eventData.Context);
            return Task.CompletedTask;
        }

        public void ResetState()
        {
            _triggerSession?.Dispose();
            _triggerSession = null;
            _parallelSaveChangesCount = 0;
            _afterSaveFailedTriggersRaisedDepth = 0;
#if DEBUG
            _capturedDbContext = null;
#endif
        }

        public Task ResetStateAsync(CancellationToken cancellationToken = default)
        {
            ResetState();
            return Task.CompletedTask;
        }

        private void RaiseAfterSaveFailedTriggers(Exception exception)
        {
            Debug.Assert(_triggerSession != null);

            _triggerSession.RaiseAfterSaveFailedStartingTriggers(exception);
            _triggerSession.RaiseAfterSaveFailedTriggers(exception);
            _triggerSession.RaiseAfterSaveFailedCompletedTriggers(exception);
        }

        private async Task RaiseAfterSaveFailedAsyncTriggers(Exception exception, CancellationToken cancellationToken)
        {
            Debug.Assert(_triggerSession != null);

            _triggerSession.RaiseAfterSaveFailedStartingTriggers(exception);
            await _triggerSession.RaiseAfterSaveFailedStartingAsyncTriggers(exception, cancellationToken).ConfigureAwait(false);

            _triggerSession.RaiseAfterSaveFailedTriggers(exception);
            await _triggerSession.RaiseAfterSaveFailedAsyncTriggers(exception, cancellationToken).ConfigureAwait(false);

            _triggerSession.RaiseAfterSaveFailedCompletedTriggers(exception);
            await _triggerSession.RaiseAfterSaveFailedCompletedAsyncTriggers(exception, cancellationToken).ConfigureAwait(false);
        }
    }
#pragma warning restore CS0618 // Type or member is obsolete
}

using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class EmailOutboxRepository : AbstractRepository<EmailOutbox>, IEmailOutboxRepository
    {
        public EmailOutboxRepository(EventContext context) : base(context) { }

        public async Task<List<EmailOutbox>> GetPendingBatch(int limit, DateTime now)
        {
            try
            {
                return await _context.EmailOutbox
                    .Where(e => e.Status == "Pending" && e.SendAfter <= now)
                    .OrderBy(e => e.CreatedAt)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex) when (ex is not LibraryException)
            {
                throw new DatabaseException("Failed to retrieve pending email batch.", ex);
            }
        }

        // Bulk fan-out inserts hundreds of rows at once; a duplicate DedupeKey must
        // not take the whole batch down, so conflicting rows are filtered first.
        public async Task AddMany(List<EmailOutbox> messages)
        {
            if (messages.Count == 0)
                return;

            try
            {
                var keys = messages
                    .Where(m => m.DedupeKey != null)
                    .Select(m => m.DedupeKey!)
                    .ToList();

                var existing = keys.Count == 0
                    ? new List<string>()
                    : await _context.EmailOutbox
                        .Where(e => e.DedupeKey != null && keys.Contains(e.DedupeKey))
                        .Select(e => e.DedupeKey!)
                        .ToListAsync();

                var toInsert = messages
                    .Where(m => m.DedupeKey == null || !existing.Contains(m.DedupeKey))
                    .ToList();

                if (toInsert.Count == 0)
                    return;

                _context.EmailOutbox.AddRange(toInsert);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new DatabaseException($"Failed to queue emails. {ex.InnerException?.Message ?? ex.Message}", ex);
            }
        }

        public async Task<bool> ExistsByDedupeKey(string dedupeKey)
        {
            try
            {
                return await _context.EmailOutbox.AnyAsync(e => e.DedupeKey == dedupeKey);
            }
            catch (Exception ex) when (ex is not LibraryException)
            {
                throw new DatabaseException("Failed to check email dedupe key.", ex);
            }
        }
    }
}

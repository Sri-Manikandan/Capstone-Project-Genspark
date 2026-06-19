using EMSDALLibrary.Contexts;
using EMSModelLibrary.Exceptions;
using EMSDALLibrary.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public abstract class AbstractRepository<T> : IRepository<T> where T : class
    {
        protected readonly EventContext _context;

        protected AbstractRepository(EventContext context)
        {
            _context = context;
        }

        public async virtual Task<T> Add(T entity)
        {
            try
            {
                _context.Set<T>().Add(entity);
                await _context.SaveChangesAsync();
                return entity;
            }
            catch (DbUpdateException ex)
            {
                throw new DatabaseException($"Failed to save {typeof(T).Name}. {ex.InnerException?.Message ?? ex.Message}", ex);
            }
        }

        public async virtual Task<T?> GetById(int id)
        {
            try
            {
                return await _context.Set<T>().FindAsync(id);
            }
            catch (Exception ex) when (ex is not LibraryException)
            {
                throw new DatabaseException($"Failed to retrieve {typeof(T).Name} with ID {id}.", ex);
            }
        }

        public async virtual Task<List<T>> GetAll()
        {
            try
            {
                return await _context.Set<T>().ToListAsync();
            }
            catch (Exception ex) when (ex is not LibraryException)
            {
                throw new DatabaseException($"Failed to retrieve {typeof(T).Name} list.", ex);
            }
        }

        public async virtual Task<T?> Update(T entity)
        {
            try
            {
                _context.Set<T>().Update(entity);
                await _context.SaveChangesAsync();
                return entity;
            }
            catch (DbUpdateException ex)
            {
                throw new DatabaseException($"Failed to update {typeof(T).Name}. {ex.InnerException?.Message ?? ex.Message}", ex);
            }
        }

        public async virtual Task Delete(int id)
        {
            try
            {
                var entity = await GetById(id);
                if (entity == null)
                    throw new NotFoundException($"{typeof(T).Name} with ID {id} not found.");
                _context.Set<T>().Remove(entity);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new DatabaseException($"Failed to delete {typeof(T).Name} with ID {id}. {ex.InnerException?.Message ?? ex.Message}", ex);
            }
        }
    }
}

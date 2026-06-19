namespace EMSDALLibrary.Interfaces{
    public interface IRepository<T> where T : class{
        Task<T> Add(T entity);
        Task<T?> GetById(int id);
        Task<List<T>> GetAll();
        Task<T?> Update(T entity);
        Task Delete(int id);
    }
}
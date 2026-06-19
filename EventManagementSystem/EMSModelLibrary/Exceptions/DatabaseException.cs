namespace EMSModelLibrary.Exceptions
{
    public class DatabaseException : LibraryException
    {
        public DatabaseException(string message) : base(message) { }
        public DatabaseException(string message, Exception inner) : base(message, inner) { }
    }
}

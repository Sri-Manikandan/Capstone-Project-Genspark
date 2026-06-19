namespace EMSModelLibrary.Exceptions
{
    public class LibraryException : Exception
    {
        public LibraryException(string message) : base(message) { }
        public LibraryException(string message, Exception inner) : base(message, inner) { }
    }
}

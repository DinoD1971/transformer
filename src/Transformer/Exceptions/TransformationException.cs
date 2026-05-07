namespace Transformer.Exceptions;

public class TransformationException : Exception
{
    public TransformationException(string message, Exception? inner = null)
        : base(message, inner) { }
}

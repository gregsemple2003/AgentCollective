using System.Text;

public class DualTextWriter : TextWriter
{
    private readonly TextWriter _originalOut;
    private readonly StringWriter _stringWriter;

    public DualTextWriter(TextWriter originalOut)
    {
        _originalOut = originalOut;
        _stringWriter = new StringWriter();
    }

    public override void Write(char value)
    {
        _originalOut.Write(value);
        _stringWriter.Write(value);
    }

    public override void WriteLine(string value)
    {
        _originalOut.WriteLine(value);
        _stringWriter.WriteLine(value);
    }

    public override Encoding Encoding => _originalOut.Encoding;

    public string CapturedOutput => _stringWriter.ToString();
}

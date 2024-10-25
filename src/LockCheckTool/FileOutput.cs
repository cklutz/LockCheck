using System;
using System.IO;
using System.Linq;
using System.Text;

namespace LockCheckTool;

internal class FileOutput : IOutput
{
    private static readonly byte[] s_newLineBytes = Environment.NewLine.Select(c => (byte)c).ToArray();
    private readonly Stream _fileStream;

    public FileOutput(Stream fileStream)
    {
        _fileStream = fileStream;
    }

    public void Dispose()
    {
        _fileStream.Dispose();
    }

    public void Write(char c)
    {
        _fileStream.Write([(byte)c], 0, 1);
    }

    public void Write(string? text)
    {
        if (text != null)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            _fileStream.Write(bytes, 0, bytes.Length);
        }
    }

    public void WriteLine(string? text)
    {
        if (text != null)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text + Environment.NewLine);
            _fileStream.Write(bytes, 0, bytes.Length);
        }
    }

    public void WriteLine()
    {
        _fileStream.Write(s_newLineBytes, 0, s_newLineBytes.Length);
    }
}

using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Debugger.Interop;

namespace VstuBridgeDebugAdaptor.Vstu;

[ComVisible(true)]
internal class DebugDocumentPosition : IDebugDocumentPosition2
{
    public string FileName { get; }

    public int Line { get; }

    public int Column { get; }

    public DebugDocumentPosition(string fileName, int line, int column)
    {
        FileName = fileName;
        Line = line;
        Column = column;
    }

    public int GetFileName(out string pbstrFileName)
    {
        pbstrFileName = FileName;
        return 0;
    }

    public int GetDocument(out IDebugDocument2 ppDoc) => throw new NotImplementedException();

    public int IsPositionInDocument(IDebugDocument2 pDoc) => throw new NotImplementedException();

    public int GetRange(TEXT_POSITION[] pBegPosition, TEXT_POSITION[] pEndPosition)
    {
        pBegPosition[0].dwLine = (uint)(Line - 1);
        pBegPosition[0].dwColumn = (uint)Column;
        return 0;
    }
}

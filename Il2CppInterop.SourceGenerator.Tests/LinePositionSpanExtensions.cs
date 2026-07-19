using Microsoft.CodeAnalysis.Text;

namespace Il2CppInterop.SourceGenerator.Tests;

internal static class LinePositionSpanExtensions
{
    extension(LinePositionSpan)
    {
        public static LinePositionSpan FindInSource(string source, string target, int index = 0)
        {
            var searchStartPosition = 0;
            var targetStartPosition = -1;
            while (index >= 0)
            {
                targetStartPosition = source.IndexOf(target, searchStartPosition, StringComparison.Ordinal);
                if (targetStartPosition < 0)
                    throw new ArgumentException($"Target string '{target}' not found in source.", nameof(target));
                searchStartPosition = targetStartPosition + 1;
                index--;
            }

            // Calculate line and character positions
            int startLine;
            int startColumn;
            {
                var newLineCount = source.AsSpan(0, targetStartPosition).Count('\n');
                var lastNewLinePosition = newLineCount == 0 ? 0 : source.LastIndexOf('\n', targetStartPosition);
                startLine = newLineCount + 1; // Line numbers are 1-based
                startColumn = targetStartPosition - lastNewLinePosition;
            }

            int endLine;
            int endColumn;
            {
                var newLineCount = target.Count('\n');
                var lastNewLinePosition = newLineCount == 0 ? 0 : target.LastIndexOf('\n');
                endLine = startLine + newLineCount;
                endColumn = newLineCount == 0 ? startColumn + target.Length : target.Length - lastNewLinePosition;
            }

            return new LinePositionSpan(new LinePosition(startLine, startColumn), new LinePosition(endLine, endColumn));
        }
    }
}

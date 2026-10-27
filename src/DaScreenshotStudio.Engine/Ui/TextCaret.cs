using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public struct TextCaret
{
    public int Low { get; private set; }

    public int High { get; private set; }

    public bool IsReversed { get; private set; }

    public readonly int Position => IsReversed ? Low : High;

    public readonly int Anchor => IsReversed ? High : Low;

    public readonly bool HasRun => High > Low;

    public void Collapse(int at, int length) => Select(at, at, length);

    public void Select(int anchor, int moving, int length)
    {
        anchor = Math.Clamp(anchor, 0, length);
        moving = Math.Clamp(moving, 0, length);

        IsReversed = moving < anchor;
        Low = Math.Min(anchor, moving);
        High = Math.Max(anchor, moving);
    }

    public void Clamp(int length)
    {
        Low = Math.Clamp(Low, 0, length);
        High = Math.Clamp(High, 0, length);

        if (Low >= High)
        {
            IsReversed = false;
        }
    }
}

public static class TextCaretMotion
{
    public static bool IsWide(byte value) => (value & 0x80) != 0;

    public static int Left(scoped ReadOnlySpan<byte> text, int position)
    {
        if (position <= 0)
        {
            return position;
        }

        position--;
        return position < text.Length && IsWide(text[position]) ? position - 1 : position;
    }

    public static int Right(scoped ReadOnlySpan<byte> text, int position)
    {
        if (position >= text.Length)
        {
            return position;
        }

        return IsWide(text[position]) ? position + 2 : position + 1;
    }

    public static int WordLeft(scoped ReadOnlySpan<byte> text, int position)
    {
        if (position <= 0 || text.Length == 0)
        {
            return 0;
        }

        var start = Math.Min(position - 1, text.Length - 1);
        var kind = ClassOf(text[start]);
        while (start > 0 && ClassOf(text[start - 1]) == kind)
        {
            start--;
        }

        return start;
    }

    public static int WordRight(scoped ReadOnlySpan<byte> text, int position)
    {
        if (position >= text.Length)
        {
            return text.Length;
        }

        var index = WordLeft(text, position + 1);
        var kind = ClassOf(text[index]);
        while (index < text.Length && ClassOf(text[index]) == kind)
        {
            index++;
        }

        return index;
    }

    public static int PreviousBoundary(scoped ReadOnlySpan<byte> text, int position)
    {
        if (position <= 0)
        {
            return 0;
        }

        var boundary = 0;
        var index = 0;

        while (index < position)
        {
            boundary = index;
            if (index < text.Length && IsWide(text[index]))
            {
                index++;
            }

            index++;
        }

        return boundary;
    }

    public static int ForwardLength(scoped ReadOnlySpan<byte> text, int position)
    {
        if (position >= text.Length)
        {
            return 0;
        }

        return IsWide(text[position]) ? 2 : 1;
    }

    public static bool IsRunSeparator(byte value) => value is 0x20 or 0x09 or 0x0D or 0x0A;

    private static int ClassOf(byte value) => IsRunSeparator(value) ? 0 : 1;
}

public static class TextEditing
{
    public static bool IsArrow(int key) =>
        key is UiKeys.Left or UiKeys.Right or UiKeys.Up or UiKeys.Down;

    public static bool IsModified(InputModifiers modifiers) =>
        (modifiers & (InputModifiers.Control | InputModifiers.Alt)) != 0;

    public static void Navigate(
        ref TextCaret caret,
        TextBlock text,
        scoped ReadOnlySpan<byte> value,
        int key,
        InputModifiers modifiers,
        in PixelRect view,
        int contentTop)
    {
        int anchor;
        int moving;

        if ((modifiers & InputModifiers.Shift) != 0)
        {
            anchor = caret.Anchor;
            moving = Move(text, value, key, modifiers, caret.Position, in view, contentTop);
        }
        else if (caret.HasRun)
        {
            moving = key is UiKeys.Left or UiKeys.Up ? caret.Low : caret.High;
            anchor = moving;
        }
        else
        {
            moving = Move(text, value, key, modifiers, caret.Low, in view, contentTop);
            anchor = moving;
        }

        caret.Select(anchor, moving, value.Length);
    }

    public static int Move(
        TextBlock text,
        scoped ReadOnlySpan<byte> value,
        int key,
        InputModifiers modifiers,
        int position,
        in PixelRect view,
        int contentTop)
    {
        var control = (modifiers & InputModifiers.Control) != 0;
        var alt = (modifiers & InputModifiers.Alt) != 0;

        if (control && alt)
        {
            return position;
        }

        if (alt)
        {
            return key switch
            {
                UiKeys.Left => LineBound(text, position, toEnd: false),
                UiKeys.Right => LineBound(text, position, toEnd: true),
                UiKeys.Up => 0,
                _ => value.Length,
            };
        }

        if (control)
        {
            return key switch
            {
                UiKeys.Left => TextCaretMotion.WordLeft(value, position),
                UiKeys.Right => TextCaretMotion.WordRight(value, position),
                UiKeys.Up => ViewBound(text, position, in view, contentTop, up: true),
                _ => ViewBound(text, position, in view, contentTop, up: false),
            };
        }

        return key switch
        {
            UiKeys.Left => TextCaretMotion.Left(value, position),
            UiKeys.Right => TextCaretMotion.Right(value, position),
            UiKeys.Up => LineStep(text, position, -1),
            _ => LineStep(text, position, 1),
        };
    }

    public static int LineStep(TextBlock text, int position, int delta)
    {
        ArgumentNullException.ThrowIfNull(text);

        var line = text.LineOf(position);
        var pen = text.PenOn(line, position);
        var target = Math.Clamp(line + delta, 0, Math.Max(0, text.LineCount - 1));

        return target == line ? position : text.IndexAt(target, pen);
    }

    public static int LineBound(TextBlock text, int position, bool toEnd)
    {
        ArgumentNullException.ThrowIfNull(text);

        var line = text.LineOf(position);
        return toEnd ? text.LineStart(line) + text.LineLength(line) : text.LineStart(line);
    }

    public static int ViewBound(TextBlock text, int position, in PixelRect view, int contentTop, bool up)
    {
        ArgumentNullException.ThrowIfNull(text);

        var height = text.LineHeight;
        if (height <= 0 || text.LineCount == 0)
        {
            return position;
        }

        var line = text.LineOf(position);
        var pen = text.PenOn(line, position);
        var page = Math.Max(0, (view.Height / height) - 1);

        var first = Math.Max(0, ((view.Y - contentTop) + height - 1) / height);
        var target = up ? first : Math.Min(text.LineCount - 1, first + page);

        if (target == line)
        {
            var step = Math.Max(1, page);
            target = up ? line - step : line + step;
        }

        target = Math.Clamp(target, 0, Math.Max(0, text.LineCount - 1));
        return target == line ? position : text.IndexAt(target, pen);
    }

    public static bool Insert(Span<byte> buffer, ref int length, ref TextCaret caret, byte value, int max)
    {
        if (length >= max || length >= buffer.Length)
        {
            return false;
        }

        var at = caret.Low;
        RemoveRange(buffer, ref length, caret.Low, caret.High);

        buffer[at..length].CopyTo(buffer[(at + 1)..]);
        buffer[at] = value;
        length++;

        caret.Collapse(at + 1, length);
        return true;
    }

    public static bool Backspace(Span<byte> buffer, ref int length, ref TextCaret caret)
    {
        if (caret.HasRun)
        {
            var run = caret.Low;
            RemoveRange(buffer, ref length, caret.Low, caret.High);
            caret.Collapse(run, length);
            return true;
        }

        if (caret.Low <= 0)
        {
            return false;
        }

        var boundary = TextCaretMotion.PreviousBoundary(buffer[..length], caret.Low);
        RemoveRange(buffer, ref length, boundary, caret.Low);
        caret.Collapse(boundary, length);
        return true;
    }

    public static bool DeleteForward(Span<byte> buffer, ref int length, ref TextCaret caret)
    {
        if (caret.HasRun)
        {
            var run = caret.Low;
            RemoveRange(buffer, ref length, caret.Low, caret.High);
            caret.Collapse(run, length);
            return true;
        }

        var at = caret.Low;
        var taken = TextCaretMotion.ForwardLength(buffer[..length], at);
        if (taken == 0)
        {
            return false;
        }

        RemoveRange(buffer, ref length, at, at + taken);
        caret.Collapse(at, length);
        return true;
    }

    public static int DigitPrefix(scoped ReadOnlySpan<byte> value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] < (byte)'0' || value[i] > (byte)'9')
            {
                return i;
            }
        }

        return value.Length;
    }

    public static void RemoveRange(Span<byte> buffer, ref int length, int from, int to)
    {
        from = Math.Clamp(from, 0, length);
        to = Math.Clamp(to, from, length);
        if (to == from)
        {
            return;
        }

        buffer[to..length].CopyTo(buffer[from..]);
        length -= to - from;
        buffer.Slice(length, to - from).Clear();
    }
}

public struct TextPointerState
{
    public uint PressedAt { get; set; }

    public int PressedIndex { get; set; }

    public int ClickCount { get; set; }

    public int Anchor { get; set; }

    public bool IsDragging { get; set; }
}

public static class TextPointer
{
    public const uint MultiPressWindowMs = 0x1F4;

    public static bool Press(
        ref TextCaret caret,
        ref TextPointerState state,
        TextBlock text,
        scoped ReadOnlySpan<byte> value,
        int column,
        int row,
        InputModifiers modifiers,
        uint time)
    {
        ArgumentNullException.ThrowIfNull(text);

        var index = IndexAt(text, value.Length, column, row);

        var repeated = state.PressedIndex == index
            && time != 0
            && state.PressedAt != 0
            && unchecked(time - state.PressedAt) < MultiPressWindowMs;

        state.PressedAt = time;
        state.PressedIndex = index;

        if ((modifiers & InputModifiers.Shift) != 0)
        {
            state.ClickCount = 0;
            state.Anchor = caret.Anchor;
        }
        else if (repeated)
        {
            state.ClickCount++;
            index = ExtendToRun(ref state, text, value, index);
        }
        else
        {
            state.ClickCount = 0;
            state.Anchor = index;
        }

        state.IsDragging = true;
        caret.Select(state.Anchor, index, value.Length);
        return true;
    }

    public static bool Drag(
        ref TextCaret caret,
        ref TextPointerState state,
        TextBlock text,
        scoped ReadOnlySpan<byte> value,
        int column,
        int row)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!state.IsDragging)
        {
            return false;
        }

        var index = ExtendToRun(ref state, text, value, IndexAt(text, value.Length, column, row));
        caret.Select(state.Anchor, index, value.Length);
        return true;
    }

    public static bool Release(ref TextPointerState state)
    {
        if (!state.IsDragging)
        {
            return false;
        }

        state.IsDragging = false;
        return true;
    }

    public static int IndexAt(TextBlock text, int length, int column, int row)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (row < 0)
        {
            return 0;
        }

        if (text.LineHeight > 0 && row >= text.LineCount * text.LineHeight)
        {
            return length;
        }

        return Math.Min(length, text.IndexAt(text.LineAt(row), column));
    }

    private static int ExtendToRun(
        ref TextPointerState state, TextBlock text, scoped ReadOnlySpan<byte> value, int index)
    {
        if (state.ClickCount <= 0)
        {
            return index;
        }

        var (anchorLow, anchorHigh) = state.ClickCount == 1
            ? (TextCaretMotion.WordLeft(value, state.PressedIndex + 1),
               TextCaretMotion.WordRight(value, state.PressedIndex))
            : (TextEditing.LineBound(text, state.PressedIndex, toEnd: false),
               TextEditing.LineBound(text, state.PressedIndex, toEnd: true));

        var (low, high) = state.ClickCount == 1
            ? (TextCaretMotion.WordLeft(value, index + 1), TextCaretMotion.WordRight(value, index))
            : (TextEditing.LineBound(text, index, toEnd: false),
               TextEditing.LineBound(text, index, toEnd: true));

        if (anchorLow < index)
        {
            state.Anchor = anchorLow;
            return high;
        }

        state.Anchor = anchorHigh;
        return low;
    }
}

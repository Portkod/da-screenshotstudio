using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaScreenshotStudio.Engine.Staging;

namespace DaScreenshotStudio.App.ViewModels;

/// <summary>
/// One editable property of a scene object. The inspector builds these from the scene serializer's own
/// description of the object, so a part of the scene that only holds values needs no editor of its own,
/// and nothing here reflects over a type at run time.
/// </summary>
public abstract class FieldViewModel : ObservableObject
{
    private readonly IEditorHost _editor;

    protected FieldViewModel(IEditorHost editor, object owner, JsonPropertyInfo property)
    {
        _editor = editor;
        Owner = owner;
        Property = property;
        Label = FieldEditors.Words(property.Name);
    }

    public string Label { get; }

    protected object Owner { get; }

    protected JsonPropertyInfo Property { get; }

    protected object? Read() => Property.Get!(Owner);

    protected void Write(object? value)
    {
        if (Equals(value, Read()))
        {
            return;
        }

        _editor.BeginEdit($"field:{RuntimeHelpers.GetHashCode(Owner)}:{Property.Name}");
        Property.Set!(Owner, value);
        OnPropertyChanged(nameof(TextFieldViewModel.Value));
        _editor.SceneChanged();
    }
}

public sealed class TextFieldViewModel(IEditorHost editor, object owner, JsonPropertyInfo property)
    : FieldViewModel(editor, owner, property)
{
    public string Value
    {
        get => (string?)Read() ?? "";
        set => Write(value ?? "");
    }
}

public sealed class NumberFieldViewModel : FieldViewModel
{
    private readonly Type _type;

    public NumberFieldViewModel(IEditorHost editor, object owner, JsonPropertyInfo property)
        : base(editor, owner, property)
    {
        _type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        IsOptional = Nullable.GetUnderlyingType(property.PropertyType) is not null;
        (Minimum, Maximum) = Type.GetTypeCode(_type) switch
        {
            TypeCode.Byte => ((decimal)byte.MinValue, (decimal)byte.MaxValue),
            TypeCode.SByte => (sbyte.MinValue, sbyte.MaxValue),
            TypeCode.Int16 => (short.MinValue, short.MaxValue),
            TypeCode.UInt16 => (ushort.MinValue, ushort.MaxValue),
            TypeCode.UInt32 => (uint.MinValue, uint.MaxValue),
            _ => ((decimal)int.MinValue, (decimal)int.MaxValue),
        };
    }

    public decimal Minimum { get; }

    public decimal Maximum { get; }

    /// <summary>Whether blank is a value of its own, which a nullable property means.</summary>
    public bool IsOptional { get; }

    public decimal? Value
    {
        get => Read() is { } value ? Convert.ToDecimal(value, CultureInfo.InvariantCulture) : null;
        set => Write(value is { } number
            ? Convert.ChangeType(Math.Clamp(Math.Round(number), Minimum, Maximum), _type, CultureInfo.InvariantCulture)
            : IsOptional ? null : Convert.ChangeType(0, _type, CultureInfo.InvariantCulture));
    }
}

public sealed class BoolFieldViewModel(IEditorHost editor, object owner, JsonPropertyInfo property)
    : FieldViewModel(editor, owner, property)
{
    public bool Value
    {
        get => (bool)Read()!;
        set => Write(value);
    }
}

public sealed class ChoiceFieldViewModel(IEditorHost editor, object owner, JsonPropertyInfo property)
    : FieldViewModel(editor, owner, property)
{
    /// <summary>Every named value, read as the underlying numbers so no enum array is made at run time.</summary>
    public IReadOnlyList<object> Choices { get; } =
        [.. Enum.GetValuesAsUnderlyingType(property.PropertyType).Cast<object>().Select(v => Enum.ToObject(property.PropertyType, v))];

    public object? Value
    {
        get => Read();
        set
        {
            if (value is not null)
            {
                Write(value);
            }
        }
    }
}

/// <summary>A property holding an object of its own, edited in place under its label.</summary>
public sealed class GroupFieldViewModel(IEditorHost editor, object owner, JsonPropertyInfo property)
    : FieldViewModel(editor, owner, property)
{
    public IReadOnlyList<FieldViewModel> Fields { get; } = FieldEditors.For(editor, property.Get!(owner)!);
}

/// <summary>A list of records, each edited in place, with a way to add and remove them.</summary>
public sealed partial class ListFieldViewModel : FieldViewModel
{
    private readonly IEditorHost _editor;
    private readonly IList _list;
    private readonly JsonTypeInfo _itemType;

    public ListFieldViewModel(IEditorHost editor, object owner, JsonPropertyInfo property, Type itemType)
        : base(editor, owner, property)
    {
        _editor = editor;
        _list = (IList)property.Get!(owner)!;
        _itemType = SceneDocument.TypeInfo(itemType);
        foreach (var item in _list)
        {
            Items.Add(new RecordViewModel(this, editor, item!));
        }
    }

    public ObservableCollection<RecordViewModel> Items { get; } = [];

    [RelayCommand]
    private void AddItem()
    {
        _editor.BeginEdit($"list:{RuntimeHelpers.GetHashCode(_list)}:add");

        // A new record copies the last one, which is what a run of similar rows wants.
        var item = _list.Count > 0
            ? JsonSerializer.Deserialize(JsonSerializer.Serialize(_list[^1], _itemType), _itemType)!
            : _itemType.CreateObject!();
        _list.Add(item);
        Items.Add(new RecordViewModel(this, _editor, item));
        _editor.SceneChanged();
    }

    public void Remove(RecordViewModel record)
    {
        var index = Items.IndexOf(record);
        if (index < 0)
        {
            return;
        }

        _editor.BeginEdit($"list:{RuntimeHelpers.GetHashCode(_list)}:remove:{index}");
        _list.RemoveAt(index);
        Items.RemoveAt(index);
        _editor.SceneChanged();
    }
}

/// <summary>One record of a <see cref="ListFieldViewModel"/>.</summary>
public sealed partial class RecordViewModel(ListFieldViewModel owner, IEditorHost editor, object model)
{
    public IReadOnlyList<FieldViewModel> Fields { get; } = FieldEditors.For(editor, model);

    [RelayCommand]
    private void Remove() => owner.Remove(this);
}

public static class FieldEditors
{
    /// <summary>
    /// An editor for every read-write property of a scene object, in declaration order. A byte array is
    /// left out, because it holds a file rather than a value.
    /// </summary>
    public static List<FieldViewModel> For(IEditorHost editor, object model, IReadOnlyCollection<string>? only = null)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(model);

        var fields = new List<FieldViewModel>();
        foreach (var property in SceneDocument.TypeInfo(model.GetType()).Properties)
        {
            if (property.Get is null || property.Set is null || (only is not null && !only.Contains(property.Name)))
            {
                continue;
            }

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            FieldViewModel? field = type switch
            {
                _ when type == typeof(string) => new TextFieldViewModel(editor, model, property),
                _ when type == typeof(bool) => new BoolFieldViewModel(editor, model, property),
                _ when type.IsEnum => new ChoiceFieldViewModel(editor, model, property),
                _ when type.IsPrimitive && type != typeof(char) && type != typeof(double) && type != typeof(float) =>
                    new NumberFieldViewModel(editor, model, property),
                _ when type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)
                    && type.GetGenericArguments()[0] is { IsClass: true } item && item != typeof(string) =>
                    new ListFieldViewModel(editor, model, property, item),
                _ when type.IsClass && type != typeof(byte[]) && property.Get(model) is not null =>
                    new GroupFieldViewModel(editor, model, property),
                _ => null,
            };

            if (field is not null)
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    /// <summary>A property name as words: <c>WorldCount</c> reads "World count".</summary>
    public static string Words(string name)
    {
        var text = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
            {
                text.Append(' ').Append(char.ToLowerInvariant(name[i]));
            }
            else
            {
                text.Append(name[i]);
            }
        }

        return text.ToString();
    }
}

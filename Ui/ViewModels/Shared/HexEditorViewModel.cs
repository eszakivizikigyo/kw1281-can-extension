using System;
using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.Shared;

public partial class HexEditorViewModel : ViewModelBase
{
    private byte[] _data = [];

    [ObservableProperty]
    private string _statusText = "No data loaded.";

    public ObservableCollection<HexRow> Rows { get; } = [];

    public void LoadData(byte[] data, uint baseAddress = 0)
    {
        _data = data;
        Rows.Clear();

        for (var offset = 0; offset < data.Length; offset += 16)
        {
            var count = Math.Min(16, data.Length - offset);
            var rowBytes = new byte[count];
            Array.Copy(data, offset, rowBytes, 0, count);
            Rows.Add(new HexRow((uint)(baseAddress + offset), rowBytes));
        }

        StatusText = $"{data.Length} bytes loaded.";
    }

    [RelayCommand]
    private void Clear()
    {
        _data = [];
        Rows.Clear();
        StatusText = "No data loaded.";
    }
}

public class HexRow
{
    public uint Address { get; }
    public string AddressText { get; }
    public string HexText { get; }
    public string AsciiText { get; }

    public HexRow(uint address, byte[] bytes)
    {
        Address = address;
        AddressText = $"{address:X8}";

        var hex = new StringBuilder(48);
        var ascii = new StringBuilder(16);

        for (var i = 0; i < 16; i++)
        {
            if (i < bytes.Length)
            {
                hex.Append($"{bytes[i]:X2} ");
                ascii.Append(bytes[i] is >= 0x20 and < 0x7F ? (char)bytes[i] : '.');
            }
            else
            {
                hex.Append("   ");
                ascii.Append(' ');
            }

            if (i == 7) hex.Append(' ');
        }

        HexText = hex.ToString().TrimEnd();
        AsciiText = ascii.ToString();
    }
}

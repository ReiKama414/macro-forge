namespace MacroForge.Core.Hid;

public sealed class ParsedHidDescriptor
{
    public int ButtonCount { get; init; }
    public bool HasWheel { get; init; }
    public bool HasHorizontalWheel { get; init; }
    public ushort UsagePage { get; init; }
    public ushort Usage { get; init; }
    public IReadOnlyList<int> ButtonUsages { get; init; } = Array.Empty<int>();
    public IReadOnlyList<(ushort Page, ushort Usage, string Kind)> Collections { get; init; } = Array.Empty<(ushort, ushort, string)>();
}

/// <summary>
/// Brand-agnostic HID report descriptor parser. Button counts come from Usage Page 0x09,
/// never from a vendor-specific table.
/// </summary>
public static class HidReportDescriptorParser
{
    public static ParsedHidDescriptor Parse(byte[] descriptor)
    {
        int buttonCount = 0;
        var buttonUsages = new SortedSet<int>();
        bool hasWheel = false;
        bool hasHWheel = false;
        ushort usagePage = 0;
        ushort usage = 0;
        ushort currentPage = 0;
        ushort? pendingUsage = null;
        var collections = new List<(ushort, ushort, string)>();
        var usageMin = 0;
        var usageMax = 0;
        var reportCount = 0;

        int i = 0;
        while (i < descriptor.Length)
        {
            var prefix = descriptor[i++];
            if (prefix == 0xFE)
            {
                if (i + 2 > descriptor.Length) break;
                var size = descriptor[i++];
                i += 1 + size;
                continue;
            }

            var sizeCode = prefix & 0x03;
            var type = (prefix >> 2) & 0x03;
            var tag = prefix >> 4;
            var dataSize = sizeCode == 3 ? 4 : sizeCode;
            if (i + dataSize > descriptor.Length) break;
            int data = 0;
            for (var b = 0; b < dataSize; b++)
                data |= descriptor[i++] << (8 * b);

            if (type == 1) // global
            {
                if (tag == 0) currentPage = (ushort)data;
            }
            else if (type == 2) // local
            {
                switch (tag)
                {
                    case 0:
                        pendingUsage = (ushort)data;
                        if (currentPage == 0x09)
                            buttonUsages.Add(data);
                        break;
                    case 1:
                        usageMin = data;
                        break;
                    case 2:
                        usageMax = data;
                        if (currentPage == 0x09)
                        {
                            for (var u = usageMin; u <= usageMax; u++)
                                buttonUsages.Add(u);
                        }
                        break;
                }
            }
            else if (type == 0) // main
            {
                if (tag == 8) // Input
                {
                    if (currentPage == 0x01 && pendingUsage == 0x38)
                        hasWheel = true;
                    if (currentPage == 0x01 && pendingUsage == 0x48)
                        hasHWheel = true;
                    if (currentPage == 0x0C && pendingUsage == 0x0238)
                        hasHWheel = true;
                    if (currentPage == 0x09 && buttonUsages.Count == 0 && reportCount > 0)
                    {
                        for (var u = 1; u <= reportCount; u++)
                            buttonUsages.Add(u);
                    }
                }
                else if (tag == 10) // Collection
                {
                    var kind = data switch
                    {
                        1 => "Application",
                        0 => "Physical",
                        2 => "Logical",
                        3 => "Report",
                        _ => $"Type {data}"
                    };
                    var colUsage = pendingUsage ?? 0;
                    collections.Add((currentPage, colUsage, kind));
                    if (usagePage == 0 && currentPage != 0)
                    {
                        usagePage = currentPage;
                        usage = colUsage;
                    }
                }

                pendingUsage = null;
            }

            if (type == 1 && tag == 9)
                reportCount = data;
        }

        buttonCount = buttonUsages.Count;
        return new ParsedHidDescriptor
        {
            ButtonCount = buttonCount,
            HasWheel = hasWheel,
            HasHorizontalWheel = hasHWheel,
            UsagePage = usagePage,
            Usage = usage,
            ButtonUsages = buttonUsages.ToList(),
            Collections = collections
        };
    }
}

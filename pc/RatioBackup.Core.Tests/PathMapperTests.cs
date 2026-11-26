namespace RatioBackup.Core.Tests;

public class PathMapperTests
{
    [Fact] public void Plain() => Assert.Equal(@"DCIM\Camera\IMG_1.jpg", PathMapper.ToWindowsRelative("DCIM/Camera/IMG_1.jpg"));
    [Fact] public void EscapesInvalidChars() => Assert.Equal(@"Download\a%3Ab%3F.txt", PathMapper.ToWindowsRelative("Download/a:b?.txt"));
    [Fact] public void EscapesPercentItself() => Assert.Equal(@"Download\100%25.txt", PathMapper.ToWindowsRelative("Download/100%.txt"));
    [Fact] public void EscapesTrailingDotAndSpace() => Assert.Equal(@"Download\x%2E\y%20", PathMapper.ToWindowsRelative("Download/x./y "));
    [Fact] public void EscapesReservedNames() => Assert.Equal(@"Download\%43ON.txt", PathMapper.ToWindowsRelative("Download/CON.txt"));
    [Fact] public void EscapesReservedNamesCaseInsensitive() => Assert.Equal(@"Download\%6Eul", PathMapper.ToWindowsRelative("Download/nul"));
    [Fact] public void KeepsUnicode() => Assert.Equal(@"Pictures\Übersee ☀.jpg", PathMapper.ToWindowsRelative("Pictures/Übersee ☀.jpg"));

    [Theory]
    [InlineData("../x")]
    [InlineData("/abs")]
    [InlineData("DCIM/../../x")]
    [InlineData("")]
    [InlineData("DCIM//x")]
    [InlineData("C:/x")]
    [InlineData("DCIM/./x")]
    [InlineData("DCIM\\x")]
    [InlineData("DCIM/x\0y")]
    public void RejectsUnsafe(string p) => Assert.False(PathMapper.IsSafe(p));

    [Fact] public void AcceptsNormal() => Assert.True(PathMapper.IsSafe("Android/media/com.whatsapp/WhatsApp/Media/a.jpg"));
}

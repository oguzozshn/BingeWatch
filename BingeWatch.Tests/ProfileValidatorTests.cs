using BingeWatch.API.Dtos;
using BingeWatch.API.Services;
using Xunit;

namespace BingeWatch.Tests
{
    public class ProfileValidatorTests
    {
        private static UpdateProfileRequest Request(
            string? displayName = "Ali",
            string? bio = null,
            string? avatarUrl = null,
            bool isPrivate = false) =>
            new()
            {
                DisplayName = displayName,
                Bio = bio,
                AvatarUrl = avatarUrl,
                IsPrivate = isPrivate
            };

        [Fact]
        public void TrimsAndKeepsValues()
        {
            var ok = ProfileValidator.TryNormalize(
                Request(displayName: "  Ali Veli  ", bio: "  merhaba  ", avatarUrl: " https://x/y.png "),
                out var clean, out var error);

            Assert.True(ok);
            Assert.Null(error);
            Assert.Equal("Ali Veli", clean.DisplayName);
            Assert.Equal("merhaba", clean.Bio);
            Assert.Equal("https://x/y.png", clean.AvatarUrl);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RejectsEmptyDisplayName(string? displayName)
        {
            var ok = ProfileValidator.TryNormalize(Request(displayName), out _, out var error);

            Assert.False(ok);
            Assert.NotNull(error);
        }

        /// <summary>Boş metin ile null aynı şey: "girilmemiş".</summary>
        [Fact]
        public void BlankBioAndAvatarBecomeNull()
        {
            var ok = ProfileValidator.TryNormalize(
                Request(bio: "   ", avatarUrl: "  "), out var clean, out _);

            Assert.True(ok);
            Assert.Null(clean.Bio);
            Assert.Null(clean.AvatarUrl);
        }

        [Fact]
        public void RejectsTooLongFields()
        {
            Assert.False(ProfileValidator.TryNormalize(
                Request(displayName: new string('a', ProfileValidator.MaxDisplayNameLength + 1)), out _, out _));

            Assert.False(ProfileValidator.TryNormalize(
                Request(bio: new string('a', ProfileValidator.MaxBioLength + 1)), out _, out _));

            // Sınırın kendisi geçerli.
            Assert.True(ProfileValidator.TryNormalize(
                Request(displayName: new string('a', ProfileValidator.MaxDisplayNameLength),
                        bio: new string('a', ProfileValidator.MaxBioLength)), out _, out _));
        }

        /// <summary>
        /// Avatar adresi doğrudan <c>&lt;img src&gt;</c>'ye giriyor; javascript:
        /// ve data: şemaları saldırı yüzeyi açardı.
        /// </summary>
        [Theory]
        [InlineData("javascript:alert(1)")]
        [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
        [InlineData("file:///etc/passwd")]
        public void RejectsNonHttpAvatarSchemes(string url)
        {
            var ok = ProfileValidator.TryNormalize(Request(avatarUrl: url), out _, out var error);

            Assert.False(ok);
            Assert.NotNull(error);
        }

        [Fact]
        public void RejectsRelativeAvatarUrl()
        {
            Assert.False(ProfileValidator.TryNormalize(
                Request(avatarUrl: "/images/me.png"), out _, out _));
        }

        [Theory]
        [InlineData("http://example.com/a.png")]
        [InlineData("https://example.com/a.png")]
        public void AcceptsHttpAndHttpsAvatars(string url)
        {
            Assert.True(ProfileValidator.TryNormalize(Request(avatarUrl: url), out _, out _));
        }

        [Fact]
        public void CarriesPrivacyFlagThrough()
        {
            ProfileValidator.TryNormalize(Request(isPrivate: true), out var clean, out _);
            Assert.True(clean.IsPrivate);

            ProfileValidator.TryNormalize(Request(isPrivate: false), out var open, out _);
            Assert.False(open.IsPrivate);
        }
    }
}

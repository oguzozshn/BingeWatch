using System.Text.Json;
using BingeWatch.API.Clients;
using BingeWatch.API.Models;
using Xunit;

namespace BingeWatch.Tests
{
    public class NullableDateTimeConverterTests
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new NullableDateTimeConverter() }
        };

        [Fact]
        public void EmptyStringDate_IsTreatedAsNull_InsteadOfThrowing()
        {
            // TMDb, tarihi bilinmeyen diziler için "" döndürüyor. Converter olmadan
            // System.Text.Json burada patlıyor ve tek bir kayıt tüm aramayı çökertiyordu.
            var json = """{"first_air_date":""}""";

            var result = JsonSerializer.Deserialize<SeriesItem>(json, Options);

            Assert.NotNull(result);
            Assert.Null(result!.FirstAirDate);
        }

        [Fact]
        public void OneBadDate_DoesNotBreakTheWholeSearchResponse()
        {
            // Asıl regresyon: results dizisindeki tek bir boş tarih, 500 hatasıyla
            // arama sonuçlarının tamamını kaybettiriyordu.
            var json = """
            {
              "page": 1,
              "results": [
                {"id": 1, "name": "Good Show", "first_air_date": "2008-01-20"},
                {"id": 2, "name": "Undated Show", "first_air_date": ""},
                {"id": 3, "name": "Another Good", "first_air_date": "2015-06-01"}
              ]
            }
            """;

            var result = JsonSerializer.Deserialize<TmdbSeriesResult>(json, Options);

            Assert.NotNull(result);
            Assert.Equal(3, result!.Results.Count);
            Assert.Equal(new DateTime(2008, 1, 20), result.Results[0].FirstAirDate);
            Assert.Null(result.Results[1].FirstAirDate);
            Assert.Equal(new DateTime(2015, 6, 1), result.Results[2].FirstAirDate);
        }

        [Fact]
        public void NullDate_StaysNull()
        {
            var json = """{"first_air_date":null}""";

            var result = JsonSerializer.Deserialize<SeriesItem>(json, Options);

            Assert.Null(result!.FirstAirDate);
        }

        [Fact]
        public void UnparseableDate_FallsBackToNull_RatherThanThrowing()
        {
            var json = """{"first_air_date":"not-a-date"}""";

            var result = JsonSerializer.Deserialize<SeriesItem>(json, Options);

            Assert.Null(result!.FirstAirDate);
        }

        [Fact]
        public void EpisodeAirDate_HandlesEmptyString()
        {
            // Bölüm tarihleri de aynı sorunu yaşıyor (yayınlanmamış bölümler).
            var json = """{"episode_number":1,"name":"TBA","air_date":""}""";

            var result = JsonSerializer.Deserialize<TmdbEpisodeDetail>(json, Options);

            Assert.NotNull(result);
            Assert.Null(result!.AirDate);
            Assert.Equal("TBA", result.Name);
        }
    }
}

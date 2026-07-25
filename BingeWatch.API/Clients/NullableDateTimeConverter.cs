using System.Text.Json;
using System.Text.Json.Serialization;

namespace BingeWatch.API.Clients
{
    /// <summary>
    /// TMDb, tarihi bilinmeyen diziler/bölümler için <c>first_air_date</c> ve
    /// <c>air_date</c> alanlarını <c>null</c> yerine <b>boş string</b> ("") olarak
    /// döndürebiliyor. System.Text.Json bunu <see cref="DateTime"/>'a çeviremeyip
    /// istisna fırlatır ve tek bir kayıt yüzünden tüm arama yanıtı çöker.
    /// Bu converter boş/geçersiz değerleri <c>null</c> kabul eder.
    /// </summary>
    public class NullableDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (string.IsNullOrWhiteSpace(value))
                    return null;

                if (DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var parsed))
                    return parsed;

                return null;
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd"));
            else
                writer.WriteNullValue();
        }
    }
}

using System.Runtime.CompilerServices;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;

namespace DynamoDb.ExpressionMapping.Tests.Snapshots;

/// <summary>
/// Module initializer that configures Verify for snapshot testing.
/// Registers custom converters for expression result types and AttributeValue.
/// </summary>
public static class VerifyConfig
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.SortPropertiesAlphabetically();

        VerifierSettings.AddExtraSettings(settings =>
        {
            settings.Converters.Add(new AttributeValueJsonConverter());
        });
    }
}

/// <summary>
/// JSON converter that serialises <see cref="AttributeValue"/> into a human-readable form
/// showing only the populated type field (S, N, BOOL, L, M, SS, NS, BS, NULL).
/// </summary>
internal sealed class AttributeValueJsonConverter : Argon.JsonConverter<AttributeValue>
{
    public override void WriteJson(Argon.JsonWriter writer, AttributeValue? value, Argon.JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();

        if (value.S is not null)
        {
            writer.WritePropertyName("S");
            writer.WriteValue(value.S);
        }
        else if (value.N is not null)
        {
            writer.WritePropertyName("N");
            writer.WriteValue(value.N);
        }
        else if (value.IsBOOLSet)
        {
            writer.WritePropertyName("BOOL");
            writer.WriteValue(value.BOOL);
        }
        else if (value.NULL)
        {
            writer.WritePropertyName("NULL");
            writer.WriteValue(true);
        }
        else if (value.IsLSet)
        {
            writer.WritePropertyName("L");
            serializer.Serialize(writer, value.L);
        }
        else if (value.IsMSet)
        {
            writer.WritePropertyName("M");
            serializer.Serialize(writer, value.M);
        }
        else if (value.SS?.Count > 0)
        {
            writer.WritePropertyName("SS");
            serializer.Serialize(writer, value.SS);
        }
        else if (value.NS?.Count > 0)
        {
            writer.WritePropertyName("NS");
            serializer.Serialize(writer, value.NS);
        }
        else if (value.BS?.Count > 0)
        {
            writer.WritePropertyName("BS");
            serializer.Serialize(writer, value.BS);
        }
        else if (value.B is not null)
        {
            writer.WritePropertyName("B");
            writer.WriteValue(Convert.ToBase64String(value.B.ToArray()));
        }

        writer.WriteEndObject();
    }

    public override AttributeValue ReadJson(Argon.JsonReader reader, Type objectType, AttributeValue? existingValue, bool hasExistingValue, Argon.JsonSerializer serializer)
        => throw new NotSupportedException("Deserialization not supported for snapshot testing.");
}

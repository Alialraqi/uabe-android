namespace UABEAvalonia.Mesh
{
    // Vertex channel related enums. Adapted from MeshEnums.cs in UABEANext4,
    // corresponding to AssetStudio's Mesh parsing logic, covering Unity's three version formats.

    /// <summary>Unity 5 and the vertex channel format of earlier versions.</summary>
    public enum VertexChannelFormat
    {
        Float,
        Float16,
        Color,
        Byte,
        UInt32
    }

    /// <summary>Unity 2017 / 2018 Vertex format used (V1).</summary>
    public enum VertexFormatV1
    {
        Float,
        Float16,
        Color,
        UNorm8,
        SNorm8,
        UNorm16,
        SNorm16,
        UInt8,
        SInt8,
        UInt16,
        SInt16,
        UInt32,
        SInt32
    }

    /// <summary>Unity 2018+ Vertex format used (V2, current mainstream).</summary>
    public enum VertexFormatV2
    {
        Float,
        Float16,
        UNorm8,
        SNorm8,
        UNorm16,
        SNorm16,
        UInt8,
        SInt8,
        UInt16,
        SInt16,
        UInt32,
        SInt32
    }

    /// <summary>Unity 2018+ Channel type (semantics inferred from the channel index).</summary>
    public enum ChannelTypeV3
    {
        Vertex,
        Normal,
        Tangent,
        Color,
        TexCoord0,
        TexCoord1,
        TexCoord2,
        TexCoord3,
        TexCoord4,
        TexCoord5,
        TexCoord6,
        TexCoord7,
        BlendWeight,
        BlendIndices,
    }

    /// <summary>Unity 5 ~ 2017 channel type.</summary>
    public enum ChannelTypeV2
    {
        Vertex,
        Normal,
        Color,
        TexCoord0,
        TexCoord1,
        TexCoord2,
        TexCoord3,
        Tangent,
    }

    /// <summary>Channel type of earlier versions.</summary>
    public enum ChannelTypeV1
    {
        Vertex,
        Normal,
        Color,
        TexCoord0,
        TexCoord1,
        Tangent,
    }
}

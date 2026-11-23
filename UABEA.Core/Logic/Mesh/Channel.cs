using AssetsTools.NET;

namespace UABEAvalonia.Mesh
{
    /// <summary>
    /// Describes the layout info of a single vertex channel. Adapted from Channel.cs in UABEANext4.
    /// Each Channel corresponds to one element of the m_VertexData.m_Channels array.
    /// </summary>
    public class Channel
    {
        /// <summary>Stream index it belongs to. Different streams are stored separately in the vertex data.</summary>
        public byte stream;
        /// <summary>Byte offset of this channel within one vertex record.</summary>
        public byte offset;
 /// <summary>Channel format (exact meaning varies with the Unity version, see VertexChannelFormat / VertexFormatV1 / VertexFormatV2). </summary>
        public byte format;
        /// <summary>Channel dimension (low 4 bits are valid, e.g. 3 means vec3).</summary>
        public byte dimension;

        public Channel(AssetTypeValueField field)
        {
            stream = field["stream"].AsByte;
            offset = field["offset"].AsByte;
            format = field["format"].AsByte;
            dimension = field["dimension"].AsByte;
        }
    }
}

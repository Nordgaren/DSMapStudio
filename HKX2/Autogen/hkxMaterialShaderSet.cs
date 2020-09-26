using SoulsFormats;
using System.Collections.Generic;
using System.Numerics;

namespace HKX2
{
    public class hkxMaterialShaderSet : hkReferencedObject
    {
        public List<hkxMaterialShader> m_shaders;
        
        public override void Read(PackFileDeserializer des, BinaryReaderEx br)
        {
            base.Read(des, br);
            m_shaders = des.ReadClassPointerArray<hkxMaterialShader>(br);
        }
        
        public override void Write(BinaryWriterEx bw)
        {
            base.Write(bw);
        }
    }
}
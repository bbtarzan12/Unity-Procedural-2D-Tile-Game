Shader "Custom/VertexColorUnlit" {
    SubShader {
        BindChannels {
            Bind "Color", color
            Bind "Vertex", vertex
            Bind "TexCoord", texcoord
        }
        Pass {
        }
    }
}
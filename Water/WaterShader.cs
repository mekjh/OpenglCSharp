using OpenGL;
using glEng.Shader;

namespace glEng.Water
{
    public class WaterShader : ShaderProgram
    {
        const string VERTEX_FILE = @"\Water\glsl\_vert.glsl";
        const string FRAGMENT_FILE = @"\Water\glsl\_frag.glsl";

        private int loc_model;
        private int loc_proj;
        private int loc_view;
        private int loc_repeatTextureDetailMap;
        private int loc_waterColor;
        private int loc_flowTexCoord;

        public WaterShader() :
            base(EngineLoop.PROJECT_PATH + VERTEX_FILE,
            EngineLoop.PROJECT_PATH + FRAGMENT_FILE, "", "", "")
        { }

        protected override void bindAttributes()
        {
            base.bindAttribute(0, "position");
            base.bindAttribute(1, "texCoord");
        }

        protected override void getAllUniformLocations()
        {
            loc_model = base.getUniformLocation("model");
            loc_proj = base.getUniformLocation("proj");
            loc_view = base.getUniformLocation("view");
            loc_repeatTextureDetailMap = base.getUniformLocation("repeatTextureDetailMap");
            loc_waterColor = base.getUniformLocation("waterColor");
            loc_flowTexCoord = base.getUniformLocation("flowVector");

        }

        public void LoadFlowVector2(float tu, float tv)
        {
            base.loadVector(loc_flowTexCoord, new Vertex2f(tu, tv));
        }

        public void LoadRepeatTextureDetailMap(float repeatTextureDetailMap)
        {
            base.loadFloat(loc_repeatTextureDetailMap, repeatTextureDetailMap);
        }

        public void LoadWaterColor(Vertex3f color)
        {
            base.loadVector(loc_waterColor, color);
        }

        public void LoadProjMatrix(Matrix4x4f matrix)
        {
            base.loadMatrix(loc_proj, matrix);
        }

        public void LoadViewMatrix(Matrix4x4f matrix)
        {
            base.loadMatrix(loc_view, matrix);
        }

        public void LoadModelMatrix(Matrix4x4f matrix)
        {
            base.loadMatrix(loc_model, matrix);
        }

        public void LoadTexture(uint textureUnit, string textureName)
        {
            int location = base.getUniformLocation(textureName);
            base.loadInt(location, (int)textureUnit);
        }

    }
}

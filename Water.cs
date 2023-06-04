using OpenGL;
using System;
using System.Collections.Generic;

namespace glEng.Water
{
    public class Water
    {       
        int WATER_RESOLUTION = 100;
        
        // 시스템 구현 변수
        Cameras.OnePersonCamera camera;
        WaterShader shader;
        BVHierarchy.AABB aabb;

        public uint vao;
        public uint vbo;
        public uint ebo;

        Matrix4x4f worldMatrix;
        Model.Texture texID;
        int numVertices;
        int numIndices;

        // 버퍼
        Vertex3f[] normalArray;
        Vertex3f[] vertextArray;
        Vertex2f[] texCoordBuffer;
        float[] forceArray;
        float[] velocityArray;
        int[] polyIndexArray;

        // 속성 변수
        Vertex3f color;
        float transparency;

        float centerX;
        float centerZ;
        float edgeLength;
        float waterLevel;

        // 물의 흐름을 위한 변수
        float flowVelocity = 0.002f;
        float flowTv = 0.0f;
        float flowTu = 0.0f;
        Vertex2f flowDirection;


        #region 속성

        public float Size
        {
            get
            {
                return edgeLength * 2 + 1;
            }
        }

        public float FlowVelocity
        {
            get
            {
                return this.flowVelocity;
            }

            set
            {
                this.flowVelocity = value;
            }
        }

        public Vertex2f FlowDirection
        {
            get
            {
                return this.flowDirection;
            }

            set
            {
                this.flowDirection = value;
            }
        }

        public int NumVertices
        {
            get
            {
                return numVertices;
            }
        }

        public int NumIndices
        {
            get
            {
                return this.numIndices;
            }
        }
        #endregion

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="centerX">지형의 중심</param>
        /// <param name="centerZ">지형의 중심</param>
        /// <param name="edgeLength">지형의 전체 half 너비</param>
        /// <param name="altitude">물이 위치할 고도</param>
        public Water(float centerX, float centerZ, float edgeLength, float waterLevel)
        {
            // 시스템 초기화
            this.shader = new WaterShader();

            // 속성 초기화
            this.centerX = centerX;
            this.centerZ = centerZ;
            this.edgeLength = edgeLength;
            this.waterLevel = waterLevel;

            // 10m당 하나의 quad를 사용하기 위한 물의 해상도 설정
            WATER_RESOLUTION = (int)Math.Ceiling(edgeLength / 10.0f) + 1;

            this.color = new Vertex3f(1.0f, 1.0f, 1.0f);
            this.transparency = 1.0f;
            this.flowDirection = new Vertex2f(1.0f, 0.0f);

            // initialize
            normalArray = new Vertex3f[WATER_RESOLUTION * WATER_RESOLUTION];
            vertextArray = new Vertex3f[WATER_RESOLUTION * WATER_RESOLUTION];
            texCoordBuffer = new Vertex2f[WATER_RESOLUTION * WATER_RESOLUTION];
            forceArray = new float[WATER_RESOLUTION * WATER_RESOLUTION];
            velocityArray = new float[WATER_RESOLUTION * WATER_RESOLUTION];
            polyIndexArray = new int[(WATER_RESOLUTION - 1) * (WATER_RESOLUTION - 1) * 6];

            // AABB_BOX 설정
            aabb = new BVHierarchy.AABB();
            Vertex3f lowwerBound = new Vertex3f(centerX - edgeLength, waterLevel - 30, centerZ - edgeLength);
            Vertex3f upperBound = new Vertex3f(centerX + edgeLength, waterLevel + 30, centerZ + edgeLength);
            aabb.FitBox(lowwerBound, upperBound);

            // gpu load
            // this.PushGpuData();
            this.Init();
        }

        /// <summary>
        /// 물의 색상을 지정합니다. 
        /// </summary>
        /// <param name="red"></param>
        /// <param name="green"></param>
        /// <param name="blue"></param>
        /// <param name="alpha">알파값은 1.0이 투명한 값</param>
        public void SetColor(float red, float green, float blue, float alpha)
        {
            this.color = new Vertex3f(red, green, blue);
            this.transparency = alpha;
        }

        /// <summary>
        /// 물의 텍스쳐를 지정하여 로딩한다.
        /// </summary>
        /// <param name="fileName">절대경로</param>
        public void LoadTextureMaps(string fileName)
        {
            this.texID = new Model.Texture(fileName, Assimp.TextureType.Diffuse, true);
        }

        /// <summary>
        /// 업데이트한다. 
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="deltaTime"></param>
        public void Update(Cameras.OnePersonCamera camera, int deltaTime)
        {
            CleanGPU();
            PushGpuData2();

            this.camera = camera;
            this.worldMatrix = Matrix4x4f.Identity;
            this.worldMatrix[3, 0] = this.centerX;
            this.worldMatrix[3, 1] = this.waterLevel;
            this.worldMatrix[3, 2] = this.centerZ;

            //"animate" the water and velocity
            float deltaTimeSecond = 0.001f * (float)deltaTime;
            float deltaMovedDistancedTexcoord = this.flowVelocity * deltaTimeSecond;
            flowTu += deltaMovedDistancedTexcoord;
            flowTv += deltaMovedDistancedTexcoord;
        }

        public void Update2(Cameras.OnePersonCamera camera, int deltaTime)
        {
            CleanGPU();
            PushGpuData2();

            float delta = deltaTime * 0.000001f;

            // 업데이트를 위한 초기설정
            this.camera = camera;
            this.worldMatrix = Matrix4x4f.Identity;
            this.worldMatrix[3, 0] = this.centerX;
            this.worldMatrix[3, 1] = this.waterLevel;
            this.worldMatrix[3, 2] = this.centerZ;

            //"animate" the water and velocity
            float deltaTimeSecond = 0.001f * (float)deltaTime;
            float deltaMovedDistancedTexcoord = this.flowVelocity * deltaTimeSecond;
            flowTu += deltaMovedDistancedTexcoord;
            flowTv += deltaMovedDistancedTexcoord;

            // calculate the current forces acting upon water.
            float d;
            int index;
            for (int z = 1; z < WATER_RESOLUTION - 1; z++)
            {
                for (int x = 1; x < WATER_RESOLUTION - 1; x++)
                {
                    float tempF = forceArray[z * WATER_RESOLUTION + x];
                    Vertex3f vert = vertextArray[z * WATER_RESOLUTION + x];

                    // bottom
                    index = (z - 1) * WATER_RESOLUTION + (x + 0);
                    d = vert.y - vertextArray[index].y;
                    forceArray[index] += d;
                    tempF -= d;

                    // left
                    index = (z + 0) * WATER_RESOLUTION + (x - 1);
                    d = vert.y - vertextArray[index].y;
                    forceArray[index] += d;
                    tempF -= d;

                    // top
                    index = (z + 1) * WATER_RESOLUTION + (x + 0);
                    d = vert.y - vertextArray[index].y;
                    forceArray[index] += d;
                    tempF -= d;

                    // right
                    index = (z + 0) * WATER_RESOLUTION + (x + 1);
                    d = vert.y - vertextArray[index].y;
                    forceArray[index] += d;
                    tempF -= d;

                    // upper right
                    index = (z + 1) * WATER_RESOLUTION + (x + 1);
                    d = (vert.y - vertextArray[index].y) * 4.94974747f;
                    forceArray[index] += d;
                    tempF -= d;

                    // lower left
                    index = (z - 1) * WATER_RESOLUTION + (x - 1);
                    d = (vert.y - vertextArray[index].y) * 4.94974747f;
                    forceArray[index] += d;
                    tempF -= d;

                    // lower right
                    index = (z - 1) * WATER_RESOLUTION + (x + 1);
                    d = (vert.y - vertextArray[index].y) * 4.94974747f;
                    forceArray[index] += d;
                    tempF -= d;

                    // upper left
                    index = (z + 1) * WATER_RESOLUTION + (x - 1);
                    d = (vert.y - vertextArray[index].y) * 4.94974747f;
                    forceArray[index] += d;
                    tempF -= d;

                    forceArray[z * WATER_RESOLUTION + x] = tempF;
                }
            }

            // calculate velocity, and update the poly field
            for (int i = 0; i < numVertices; i++)
            {
                velocityArray[i] += forceArray[i] * delta;
                vertextArray[i].y += velocityArray[i];
                forceArray[i] = 0.0f;
            }
        }

        public void CalcNormals()
        {

        }

        public void LoadReflectionMap(string FileName)
        {

        }

        public void Render2(PolygonMode polygonMode)
        {
            // 카메라 공간 충돌 테스트
            if (this.aabb.FrustumAABBIntersect(camera.Frustum)
                == BVHierarchy.AABB.COLLISION.OUTSIDE) return;

            //bind the water's "water map"
            Gl.Enable(EnableCap.Blend);
            Gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);

            Gl.PolygonMode(MaterialFace.FrontAndBack, polygonMode);
            //Gl.FrontFace(FrontFaceDirection.Ccw);

            Gl.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapS, Gl.REPEAT);
            Gl.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapT, Gl.REPEAT);

            this.shader.Bind();
            this.shader.LoadProjMatrix(DisplayManager.ProjectionMatrix);
            this.shader.LoadViewMatrix(camera.CreateViewMatrix());
            this.shader.LoadModelMatrix(this.worldMatrix);
            this.shader.LoadWaterColor(this.color);
            this.shader.LoadRepeatTextureDetailMap((float)this.edgeLength / 10.0f);
            this.shader.LoadFlowVector2(flowTu, flowTv);
            this.BindTexturesPackage();

            Gl.BindVertexArray(this.vao);
            Gl.EnableVertexAttribArray(0);
            Gl.EnableVertexAttribArray(1);
            Gl.DrawElements(PrimitiveType.Triangles, polyIndexArray.Length, DrawElementsType.UnsignedInt, polyIndexArray);

            this.shader.Unbind();

            Gl.Disable(EnableCap.Blend);
        }

        /// <summary>
        /// 매 프레임마다 렌더링한다.
        /// </summary>
        /// <param name="polygonMode"></param>
        public void Render(PolygonMode polygonMode)
        {
            //bind the water's "water map"
            Gl.Enable(EnableCap.Blend);
            Gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);

            Gl.PolygonMode(MaterialFace.Front, polygonMode);
            Gl.FrontFace(FrontFaceDirection.Ccw);
            Gl.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapS, Gl.REPEAT);
            Gl.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapT, Gl.REPEAT);

            this.shader.Bind();
            this.shader.LoadProjMatrix(DisplayManager.ProjectionMatrix);
            this.shader.LoadViewMatrix(camera.CreateViewMatrix());
            this.shader.LoadModelMatrix(this.worldMatrix);
            this.shader.LoadWaterColor(this.color);
            this.shader.LoadRepeatTextureDetailMap((float)this.edgeLength / 10.0f);
            this.shader.LoadFlowVector2(flowTu, flowTv);
            this.BindTexturesPackage();

            Gl.BindVertexArray(this.vao);
            Gl.EnableVertexAttribArray(0);
            Gl.EnableVertexAttribArray(1);
            Gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

            this.shader.Unbind();

            Gl.Disable(EnableCap.Blend);
        }

        #region 세이더에 GPU렌더링을 위한 영역

        /// <summary>
        /// 세이더에 텍스처를 로드한다.
        /// </summary>
        /// <param name="textureNumber"></param>
        /// <param name="textureName"></param>
        /// <param name="textureID"></param>
        /// <param name="mode"></param>
        private void BintTextureUnit(int textureNumber, string textureName, uint textureID, int mode = Gl.REPEAT)
        {
            Gl.ActiveTexture(TextureUnit.Texture0 + textureNumber);
            Gl.BindTexture(TextureTarget.Texture2d, textureID);
            //Gl.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, Gl.NEAREST);
            //Gl.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, Gl.NEAREST);
            Gl.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapS, mode);
            Gl.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapT, mode);
            shader.LoadTexture((uint)textureNumber, textureName);
        }

        /// <summary>
        /// 텍스처 맵을 바인딩하여 GPU에 uniform으로 사용할 수 있게 설정한다.
        /// </summary>
        private void BindTexturesPackage()
        {
            BintTextureUnit(0, "textureMap", this.texID.ID);
        }

        private void Init()
        {
            this.numVertices = (WATER_RESOLUTION * WATER_RESOLUTION);
            this.numIndices = (WATER_RESOLUTION - 1) * (WATER_RESOLUTION - 1) * 6;

            // calculate vertex location
            float unit = this.Size / (WATER_RESOLUTION - 1);
            Vertex3f dx = new Vertex3f(unit, 0, 0);
            Vertex3f dy = new Vertex3f(0, 0, unit);

            // WATER_RESOLUTION * WATER_RESOLUTION
            for (int j = 0; j < WATER_RESOLUTION; j++)
            {
                for (int k = 0; k < WATER_RESOLUTION; k++)
                {
                    vertextArray[j * WATER_RESOLUTION + k] = new Vertex3f(
                        -1.0f + dx.x * k + dy.x * j, 
                        -0.0f + dx.y * k + dy.y * j, 
                        -1.0f + dx.z * k + dy.z * j);

                    texCoordBuffer[j * WATER_RESOLUTION + k] =
                        new Vertex2f( 2.0f * ((float)k / WATER_RESOLUTION), 2.0f * ((float)j / WATER_RESOLUTION));
                }
            }

            // calculate polygon indices
            int x = 0;
            int z = WATER_RESOLUTION;
            int index = 0;
            for (int j = 0; j < WATER_RESOLUTION - 1; j++)
            {
                for (int i = 0; i < WATER_RESOLUTION - 1; i++)
                {
                    polyIndexArray[index++] = x;
                    polyIndexArray[index++] = x+1;
                    polyIndexArray[index++] = z;
                    polyIndexArray[index++] = z;
                    polyIndexArray[index++] = x+1;
                    polyIndexArray[index++] = z+1;
                    x++;
                    z++;
                }
                x++;
                z++;
            }

            Random random = new Random();
            vertextArray[(int)((float)random.NextDouble() * WATER_RESOLUTION * WATER_RESOLUTION)].y = 20.0f;
            vertextArray[(int)((float)random.NextDouble() * WATER_RESOLUTION * WATER_RESOLUTION)].y = 20.0f;
            vertextArray[(int)((float)random.NextDouble() * WATER_RESOLUTION * WATER_RESOLUTION)].y = 20.0f;
        }

        private void PushGpuData()
        {
            float[] vertices = new float[]
            {
                centerX-edgeLength, 0, centerZ-edgeLength, 0.0f, 0.0f,
                centerX +edgeLength, 0, centerZ-edgeLength, 1.0f, 0.0f,
                centerX-edgeLength, 0, centerZ+edgeLength, 0.0f, 1.0f,
                centerX+edgeLength, 0, centerZ+edgeLength, 1.0f, 1.0f,
            };

            this.vao = Gl.GenVertexArray();
            this.vbo = Gl.GenBuffer();

            Gl.BindBuffer(BufferTarget.ArrayBuffer, this.vbo);
            Gl.BufferData(BufferTarget.ArrayBuffer, (uint)(vertices.Length * sizeof(float)), vertices, BufferUsage.StaticDraw);

            Gl.BindVertexArray(this.vao);

            Gl.EnableVertexAttribArray(0);
            Gl.VertexAttribPointer(0, 3, VertexAttribType.Float, false, 5 * sizeof(float), IntPtr.Zero);
            Gl.VertexAttribPointer(1, 2, VertexAttribType.Float, false, 5 * sizeof(float), (IntPtr)(3 * sizeof(float)));

            Gl.BindVertexArray(0);
        }

        private void PushGpuData2()
        {
            float[] vertices = new float[vertextArray.Length * 5];
            float unit = 1.0f /(float)vertextArray.Length;
            for (int i = 0; i < vertextArray.Length; i++)
            {
                vertices[5 * i + 0] = vertextArray[i].x;
                vertices[5 * i + 1] = vertextArray[i].y;
                vertices[5 * i + 2] = vertextArray[i].z;
                vertices[5 * i + 3] = texCoordBuffer[i].x;
                vertices[5 * i + 4] = texCoordBuffer[i].y;
            }

            this.vao = Gl.GenVertexArray();
            this.vbo = Gl.GenBuffer();

            Gl.BindBuffer(BufferTarget.ArrayBuffer, this.vbo);
            Gl.BufferData(BufferTarget.ArrayBuffer, (uint)(vertices.Length * sizeof(float)), vertices, BufferUsage.StreamDraw);

            Gl.BindVertexArray(this.vao);

            Gl.EnableVertexAttribArray(0);
            Gl.VertexAttribPointer(0, 3, VertexAttribType.Float, false, 5 * sizeof(float), IntPtr.Zero);
            Gl.VertexAttribPointer(1, 2, VertexAttribType.Float, false, 5 * sizeof(float), (IntPtr)(3 * sizeof(float)));

            Gl.BindVertexArray(0);

            // indices ebo
            this.ebo = Gl.GenBuffer();
            Gl.BindBuffer(BufferTarget.ElementArrayBuffer, this.ebo);
            Gl.BufferData(BufferTarget.ElementArrayBuffer, (uint)(polyIndexArray.Length * sizeof(int)), polyIndexArray, BufferUsage.StreamDraw);
        }

        public void CleanGPU()
        {
            Gl.DeleteVertexArrays(this.vao);
            Gl.DeleteBuffers(this.vbo);
            Gl.DeleteBuffers(this.ebo);
        }
        #endregion

        
    }
}

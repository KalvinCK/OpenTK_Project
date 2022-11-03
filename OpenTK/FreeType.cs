using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

using FreeTypeSharp;
using  FreeTypeSharp.Native;

namespace Open_GLTK
{
    public class Fonte
    {  
        private struct Character
        {
            public int TextureID;
            public Vector2 Size;
            public Vector2i Bearing;
            public int Advance;
        }
        private int Vao, Vbo;
        private ShaderProgram shaderFonts;
        private Dictionary<uint, Character> characteres = new Dictionary<uint, Character>();
        public unsafe Fonte(string fontePath)
        {
            string vert_shader = @" #version 460 core

                                    layout (location = 0) in vec4 vertex; // <vec2 pos, vec2 tex>

                                    out vec2 TexCoords;

                                    uniform mat4 projection;

                                    void main()
                                    {
                                        gl_Position = vec4(vertex.xy, 0.0, 1.0) * projection;
                                        TexCoords = vertex.zw;
                                    }";

            string frag_shader = @" #version 460 core

                                    in vec2 TexCoords;
                                    out vec4 FragColor;

                                    uniform sampler2D text;
                                    uniform vec4 textColor;

                                    void main()
                                    {    
                                        vec4 sampled = vec4(1.0, 1.0, 1.0, texture(text, TexCoords).r);
                                        FragColor = vec4(textColor.rgb, 1.0) * sampled;
                                        if(FragColor.a <= 0.1)
                                            discard;
                                    }";
                                                
            shaderFonts = new ShaderProgram(vert_shader, frag_shader, true);

            FreeTypeFaceFacade fts = new FreeTypeFaceFacade(fontePath);
            
            fts.SetPixelSizes(0, 48);

            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

            for(uint c = 0; c < 128; c++)
            {
                fts.LoadChar(c);

                FT_Bitmap bitmap = fts.GlyphBitmap;

                int texture = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, texture);
                GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.R8, 
                (int)bitmap.width, 
                (int)bitmap.rows, 
                0, 
                PixelFormat.Red, 
                PixelType.UnsignedByte, 
                bitmap.buffer); 


                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                
                Character character = new Character();
                character.TextureID = texture;
                character.Size    = new Vector2i((int)bitmap.width, (int)bitmap.rows);
                character.Bearing = new Vector2i(fts.GlyphBitmapLeft, fts.GlyphBitmapTop);
                character.Advance = fts.GlyphMetricHorizontalAdvance;

                characteres.Add(c, character);
            }

            Vao = GL.GenVertexArray();
            GL.BindVertexArray(Vao);

            Vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeof(float) * 6 * 4, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        }
        public void RenderText(string text, Vector2 position, float scale, Color4 color)
        {
            GL.BindVertexArray(Vao);
            shaderFonts.Use();
            Matrix4 projection = Matrix4.CreateOrthographicOffCenter(0.0f, Program.Size.X, 0.0f, Program.Size.Y, 0.0f, 1.0f);
            shaderFonts.SetMatrix4("projection", projection);
            shaderFonts.SetColor4("textColor", color);
            

            foreach(var c in text)
            {
                Character ch = characteres[c];
                if(characteres.ContainsKey(c) == false)
                    continue;

                float xpos = position.X + ch.Bearing.X * scale;
                float ypos = position.Y - (ch.Size.Y - ch.Bearing.Y) * scale;

                position.X += ch.Advance * scale;

                float w = ch.Size.X * scale;
                float h = ch.Size.Y * scale;

                float[,] vertices = new float[6, 4]
                {
                    { xpos,     ypos + h,   0.0f, 0.0f },
                    { xpos,     ypos,       0.0f, 1.0f },
                    { xpos + w, ypos,       1.0f, 1.0f },

                    { xpos,     ypos + h,   0.0f, 0.0f },
                    { xpos + w, ypos,       1.0f, 1.0f },
                    { xpos + w, ypos + h,   1.0f, 0.0f }
                };
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, ch.TextureID);
                shaderFonts.SetTexture("text", 0);

                GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertices.Length * sizeof(float), vertices);


                GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
                
            }
        }
        public void Dispose()
        {
            GL.DeleteTextures(characteres.Count, characteres.Keys.ToArray());

            shaderFonts.Dispose();
            GL.DeleteBuffer(Vbo);
            GL.DeleteVertexArray(Vao);
        }
    }
    public unsafe class FreeTypeFaceFacade
    {
        // A pointer to the wrapped FreeType2 face object.
        private readonly IntPtr _Face;
        private readonly FT_FaceRec* _FaceRec;
        private readonly FreeTypeLibrary _Library;

        /// <summary>
        /// Initialize a FreeTypeFaceFacade instance with a pointer to the Face instance.
        /// </summary>
        public FreeTypeFaceFacade(string filepathname)
        {
            _Library = new FreeTypeLibrary();

            if(FT.FT_New_Face(_Library.Native, filepathname, 0, out _Face) != 0)
            {
                throw new Exception("ERROR::FREETYTPE: Failed to load font.");
            }

            _FaceRec = (FT_FaceRec*)_Face;
        }

        /// <summary>
        /// Initialize a FreeTypeFaceFacade instance with font data.
        /// </summary>
        public FreeTypeFaceFacade(FreeTypeLibrary library, IntPtr fontData, int dataLength, int faceIndex = 0)
        {
            _Library = library;

            var err = FT.FT_New_Memory_Face(_Library.Native, fontData, dataLength, faceIndex, out _Face);
            if (err != FT_Error.FT_Err_Ok)
                throw new FreeTypeException(err);

            _FaceRec = (FT_FaceRec*)_Face;
        }

        #region Properties

        public IntPtr Face { get { return _Face; } }
        public FT_FaceRec* FaceRec { get { return _FaceRec; } }

        /// <summary>
        /// Gets a value indicating whether the face has the FT_FACE_FLAG_SCALABLE flag set.
        /// </summary>
        /// <returns><see langword="true"/> if the face has the FT_FACE_FLAG_SCALABLE flag defined; otherwise, <see langword="false"/>.</returns>
        public bool HasScalableFlag { get { return HasFaceFlag(FT.FT_FACE_FLAG_SCALABLE); } }

        /// <summary>
        /// Gets a value indicating whether the face has the FT_FACE_FLAG_FIXED_SIZES flag set.
        /// </summary>
        /// <returns><see langword="true"/> if the face has the FT_FACE_FLAG_FIXED_SIZES flag defined; otherwise, <see langword="false"/>.</returns>
        public bool HasFixedSizes { get { return HasFaceFlag(FT.FT_FACE_FLAG_FIXED_SIZES); } }

        /// <summary>
        /// Gets a value indicating whether the face has the FT_FACE_FLAG_COLOR flag set.
        /// </summary>
        /// <returns><see langword="true"/> if the face has the FT_FACE_FLAG_COLOR flag defined; otherwise, <see langword="false"/>.</returns>
        public bool HasColorFlag { get { return HasFaceFlag(FT.FT_FACE_FLAG_COLOR); } }

        /// <summary>
        /// Gets a value indicating whether the face has the FT_FACE_FLAG_KERNING flag set.
        /// </summary>
        /// <returns><see langword="true"/> if the face has the FT_FACE_FLAG_KERNING flag defined; otherwise, <see langword="false"/>.</returns>
        public bool HasKerningFlag { get { return HasFaceFlag(FT.FT_FACE_FLAG_KERNING); } }

        /// <summary>
        /// Gets a value indicating whether the face has any bitmap strikes with fixed sizes.
        /// </summary>
        public bool HasBitmapStrikes { get { return (_FaceRec->num_fixed_sizes) > 0; } }

        /// <summary>
        /// Gets the current glyph bitmap.
        /// </summary>
        public FT_Bitmap GlyphBitmap { get { return _FaceRec->glyph->bitmap; } }
        public FT_Bitmap* GlyphBitmapPtr { get { return &_FaceRec->glyph->bitmap; } }

        /// <summary>
        /// Gets the left offset of the current glyph bitmap.
        /// </summary>
        public int GlyphBitmapLeft { get { return _FaceRec->glyph->bitmap_left; } }

        /// <summary>
        /// Gets the right offset of the current glyph bitmap.
        /// </summary>
        public int GlyphBitmapTop { get { return _FaceRec->glyph->bitmap_top; } }

        /// <summary>
        /// Gets the width in pixels of the current glyph.
        /// </summary>
        public int GlyphMetricWidth { get { return (int)_FaceRec->glyph->metrics.width >> 6; } }

        /// <summary>
        /// Gets the height in pixels of the current glyph.
        /// </summary>
        public int GlyphMetricHeight { get { return (int)_FaceRec->glyph->metrics.height >> 6; } }

        /// <summary>
        /// Gets the horizontal advance of the current glyph.
        /// </summary>
        public int GlyphMetricHorizontalAdvance { get { return (int)_FaceRec->glyph->metrics.horiAdvance >> 6; } }

        /// <summary>
        /// Gets the vertical advance of the current glyph.
        /// </summary>
        public int GlyphMetricVerticalAdvance { get { return (int)_FaceRec->glyph->metrics.vertAdvance >> 6; } }

        /// <summary>
        /// Gets the face's ascender size in pixels.
        /// </summary>
        public int Ascender { get { return (int)_FaceRec->size->metrics.ascender >> 6; } }

        /// <summary>
        /// Gets the face's descender size in pixels.
        /// </summary>
        public int Descender { get { return (int)_FaceRec->size->metrics.descender >> 6; } }

        /// <summary>
        /// Gets the face's line spacing in pixels.
        /// </summary>
        public int LineSpacing { get { return (int)_FaceRec->size->metrics.height >> 6; } }

        /// <summary>
        /// Gets the face's underline position.
        /// </summary>
        public int UnderlinePosition { get { return _FaceRec->underline_position >> 6; } }

        /// <summary>
        /// Gets a pointer to the face's glyph slot.
        /// </summary>
        public FT_GlyphSlotRec* GlyphSlot { get { return _FaceRec->glyph; } }

        #endregion

        #region Methods
        public void LoadChar(uint char_code)
        {
           if(FT.FT_Load_Char(_Face, char_code, FT.FT_LOAD_RENDER) != 0)
                Console.WriteLine("ERROR::FREETYTPE: Failed to load characters.");
        }

        public void SetPixelSizes(uint pixel_W, uint pixel_H) { FT.FT_Set_Pixel_Sizes(_Face, pixel_W, pixel_H); }

        /// <summary>
        /// Gets a value indicating whether the face has the specified flag defined.
        /// </summary>
        /// <param name="flag">The flag to evaluate.</param>
        /// <returns><see langword="true"/> if the face has the specified flag defined; otherwise, <see langword="false"/>.</returns>
        public bool HasFaceFlag(int flag) { return (((int)_FaceRec->face_flags) & flag) != 0; }


        /// <summary>
        /// Selects the specified character size for the font face.
        /// </summary>
        /// <param name="sizeInPoints">The size in points to select.</param>
        /// <param name="dpiX">The horizontal pixel density.</param>
        /// <param name="dpiY">The vertical pixel density.</param>
        public void SelectCharSize(int sizeInPoints, uint dpiX, uint dpiY)
        {
            var size = (IntPtr)(sizeInPoints << 6);
            var err = FT.FT_Set_Char_Size(_Face, size, size, dpiX, dpiY);
            if (err != FT_Error.FT_Err_Ok)
                throw new FreeTypeException(err);
        }

        /// <summary>
        /// Selects the specified fixed size for the font face.
        /// </summary>
        /// <param name="ix">The index of the fixed size to select.</param>
        public void SelectFixedSize(int ix)
        {
            var err = FT.FT_Select_Size(_Face, ix);
            if (err != FT_Error.FT_Err_Ok)
                throw new FreeTypeException(err);
        }

        /// <summary>
        /// Gets the glyph index of the specified character, if it is defined by this face.
        /// </summary>
        /// <param name="charCode">The character code for which to retrieve a glyph index.</param>
        /// <returns>The glyph index of the specified character, or 0 if the character is not defined by this face.</returns>
        public uint GetCharIndex(uint charCode) { return FT.FT_Get_Char_Index(_Face, charCode); }

        /// <summary>
        /// Marshals the face's family name to a C# string.
        /// </summary>
        /// <returns>The marshalled string.</returns>
        public string MarshalFamilyName() { return Marshal.PtrToStringAnsi(_FaceRec->family_name)!; }

        /// <summary>
        /// Marshals the face's style name to a C# string.
        /// </summary>
        /// <returns>The marshalled string.</returns>
        public string MarshalStyleName() { return Marshal.PtrToStringAnsi(_FaceRec->style_name)!; }

        /// <summary>
        /// Returns the specified character if it is defined by this face; otherwise, returns <see langword="null"/>.
        /// </summary>
        /// <param name="c">The character to evaluate.</param>
        /// <returns>The specified character, if it is defined by this face; otherwise, <see langword="null"/>.</returns>
        public char? GetCharIfDefined(Char c) { return FT.FT_Get_Char_Index(_Face, c) > 0 ? c : (char?)null; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetFixedSizeInPixels(FT_FaceRec* face, int ix)
        {
            return face->available_sizes[ix].height;
        }

        /// <summary>
        /// Returns the index of the fixed size which is the closest match to the specified pixel size.
        /// </summary>
        /// <param name="sizeInPixels">The desired size in pixels.</param>
        /// <param name="requireExactMatch">A value indicating whether to require an exact match.</param>
        /// <returns>The index of the closest matching fixed size.</returns>
        public int FindNearestMatchingPixelSize(int sizeInPixels, bool requireExactMatch = false)
        {
            var numFixedSizes = _FaceRec->num_fixed_sizes;
            if (numFixedSizes == 0)
                throw new InvalidOperationException("FONT_DOES_NOT_HAVE_BITMAP_STRIKES");

            var bestMatchIx = 0;
            var bestMatchDiff = Math.Abs(GetFixedSizeInPixels(_FaceRec, 0) - sizeInPixels);

            for (int i = 0; i < numFixedSizes; i++)
            {
                var size = GetFixedSizeInPixels(_FaceRec, i);
                var diff = Math.Abs(size - sizeInPixels);
                if (diff < bestMatchDiff)
                {
                    bestMatchDiff = diff;
                    bestMatchIx = i;
                }
            }

            if (bestMatchDiff != 0 && requireExactMatch)
                throw new InvalidOperationException(string.Format("NO_MATCHING_PIXEL_SIZE: {0}", sizeInPixels));

            return bestMatchIx;
        }

        public bool EmboldenGlyphBitmap(int xStrength, int yStrength)
        {
            var err = FT.FT_Bitmap_Embolden(_Library.Native, (IntPtr)(GlyphBitmapPtr), (IntPtr)xStrength, (IntPtr)yStrength);
            if (err != FT_Error.FT_Err_Ok)
                return false;

            if ((int)_FaceRec->glyph->advance.x > 0)
                _FaceRec->glyph->advance.x += xStrength;
            if ((int)_FaceRec->glyph->advance.y > 0)
                _FaceRec->glyph->advance.x += yStrength;
            _FaceRec->glyph->metrics.width += xStrength;
            _FaceRec->glyph->metrics.height += yStrength;
            _FaceRec->glyph->metrics.horiBearingY += yStrength;
            _FaceRec->glyph->metrics.horiAdvance += xStrength;
            _FaceRec->glyph->metrics.vertBearingX -= xStrength / 2;
            _FaceRec->glyph->metrics.vertBearingY += yStrength;
            _FaceRec->glyph->metrics.vertAdvance += yStrength;

            _FaceRec->glyph->bitmap_top += (yStrength >> 6);

            return true;
        }

        #endregion
    }
}
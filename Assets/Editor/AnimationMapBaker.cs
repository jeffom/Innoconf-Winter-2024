using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Codice.CM.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using VatBaker;
using VatBaker.Editor;

namespace Editor
{
    public class AnimationMapBaker
    {
        public static List<AnimationClip> GetAllAnimations(Animator animator)
        {
            List<AnimationClip> returnArray = new List<AnimationClip>();
            foreach (var anim in animator.runtimeAnimatorController.animationClips)
            {
                returnArray.Add(anim);
            }

            return returnArray;
        }

        //Return the list of baked textures containing the position, rotation and scale for animations
        public (Dictionary<SkinnedMeshRenderer, Texture2D>, Dictionary<SkinnedMeshRenderer, Texture2D> )
            BakeAnimationToTexture(GameObject target, AnimationClip anim, bool packTextures, Space space = Space.Self)
        {
            Dictionary<string, Texture2D> bakedTextures = new Dictionary<string, Texture2D>();
            int height = Mathf.FloorToInt(anim.length * anim.frameRate) + 1;
            var bakedMesh = new Mesh();
            float dt = 1f / anim.frameRate;
            Dictionary<SkinnedMeshRenderer, Texture2D> meshDict = new Dictionary<SkinnedMeshRenderer, Texture2D>();
            Dictionary<SkinnedMeshRenderer, Texture2D>
                meshNormalDict = new Dictionary<SkinnedMeshRenderer, Texture2D>();
            using var poolVtx0 = ListPool<Vector3>.Get(out var tmpVertexList);
            using var poolVtx1 = ListPool<Vector3>.Get(out var localVertices);
            using var poolNorm0 = ListPool<Vector3>.Get(out var tmpNormalList);
            using var poolNorm1 = ListPool<Vector3>.Get(out var localNormals);
            
            var meshRenderers = target.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var mr in meshRenderers)
            {
                tmpVertexList.Clear();
                tmpNormalList.Clear();
                localVertices.Clear();
                localNormals.Clear();
                using var tranScope = TransformCacheScope.ResetScope(mr.transform);
                bakedMesh = new Mesh();

                for (var y = 0; y < height; y++)
                {
                    anim.SampleAnimation(target, y*dt);
                    mr.BakeMesh(bakedMesh);

                    bakedMesh.GetVertices(tmpVertexList);
                    bakedMesh.GetNormals(tmpNormalList);

                    localVertices.AddRange(tmpVertexList);
                    localNormals.AddRange(tmpNormalList);
                }

                var trans = target.transform;
                var (vertices, normals) = space switch
                {
                    Space.Self => (
                        localVertices.Select(vtx => trans.InverseTransformPoint(vtx)),
                        localNormals.Select(norm => trans.InverseTransformDirection(norm))
                    ),
                    Space.World => (localVertices, localNormals),

                    _ => throw new ArgumentOutOfRangeException(nameof(space), space, null)
                };

                meshDict.TryAdd(mr, CreateBakeTexture(mr.sharedMesh.vertexCount, height, anim.isLooping));
                meshNormalDict.TryAdd(mr, CreateBakeTexture(mr.sharedMesh.vertexCount, height, anim.isLooping));

                meshDict[mr].SetPixels(ListToColorArray(vertices));
                meshNormalDict[mr].SetPixels(ListToColorArray(normals));
            }

            static Color[] ListToColorArray(IEnumerable<Vector3> list) =>
                list.Select(v3 => new Color(v3.x, v3.y, v3.z)).ToArray();

            return (meshDict, meshNormalDict);
        }

        Texture2D CreateBakeTexture(int w, int h, bool isLooping)
        {
            return new Texture2D(w, h, TextureFormat.RGBAHalf, false)
            {
                wrapModeU = TextureWrapMode.Clamp,
                wrapModeV = isLooping ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }

        GameObject CreatePrefab(GameObject target)
        {
            var newPrefab = PrefabUtility.SaveAsPrefabAsset(target, "BakedTextures/Output/Prefabs");
            return null;
        }

        private static Vector2[] EncodeVertexIdToUv(Mesh mesh)
        {
            // We add this offset to make sure we sample from the middle of the pixel
            float offset = 1.0f / mesh.vertexCount * 0.5f;

            Vector2[] uv = new Vector2[mesh.vertexCount];
            for (int i = 0; i < mesh.vertexCount; i++)
            {
                float uvx = Remap(i, 0, mesh.vertexCount, 0, 1) + offset;
                uv[i] = new Vector2(uvx, 1.0f);
            }

            return uv;
        }

        static float Remap(float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }
        
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int NormalTex = Shader.PropertyToID("_NormalTex");
        private static readonly int BaseShaderBumpMap = Shader.PropertyToID("_BumpMap");
        public Material GenerateMaterial(SkinnedMeshRenderer skin, Texture2D bakedAnim, Texture2D bakedNormal, string materialName, Shader shader, float fps, float animLength)
        {   
            var mat = new Material(shader)
            {
                enableInstancing = true
            };

           
            mat.SetTexture(MainTex, skin.sharedMaterial.mainTexture);
            var normalTex = skin.sharedMaterial.GetTexture(BaseShaderBumpMap);
            if (normalTex != null)
            {
                mat.SetTexture(NormalTex, normalTex);
            }

            mat.name = materialName;
            mat.SetTexture(VatShaderProperty.VatPositionTex, bakedAnim);
            mat.SetTexture(VatShaderProperty.VatNormalTex, bakedNormal);
            mat.SetFloat(VatShaderProperty.VatAnimFps, fps);
            mat.SetFloat(VatShaderProperty.VatAnimLength, animLength);

            return mat;
        }
        
        static readonly string InvalidChars = new string(Path.GetInvalidPathChars());
        static string ReplaceInvalidPathChar(string path)
        {
            return Regex.Replace(path, $"[{InvalidChars}]", "_");
        }
}
}
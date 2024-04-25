using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
        public Dictionary<string, Texture2D> BakeAnimationToTexture(GameObject target, AnimationClip anim)
        {
            Dictionary<string, Texture2D> bakedTextures = new Dictionary<string, Texture2D>();
            int height = (int)(anim.length * anim.frameRate) + 1;
            var bakedMesh = new Mesh();
            float dt = 1f / anim.frameRate;
            Vector3[] v = target.GetComponentsInChildren<SkinnedMeshRenderer>()
                .SelectMany(mf => mf.sharedMesh.vertices)
                .ToArray();

            Dictionary<Material, List<SkinnedMeshRenderer>> renderDict = new Dictionary<Material, List<SkinnedMeshRenderer>>();
            Dictionary<SkinnedMeshRenderer, Texture2D> meshDict = new Dictionary<SkinnedMeshRenderer, Texture2D>();

            var meshRenderers = target.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var mr in meshRenderers)
            {
                if (!renderDict.ContainsKey(mr.sharedMaterial))
                {
                    var list = new List<SkinnedMeshRenderer>();
                    list.Add(mr);
                    renderDict.Add(mr.sharedMaterial, list);
                }
                else
                {
                    renderDict[mr.sharedMaterial].Add(mr);
                }
            }

            foreach (var entry in renderDict)
            {
                var renderers = entry.Value;
                List<Vector3> vertices = new List<Vector3>();
                foreach (var r in renderers)
                {
                    r.BakeMesh(bakedMesh);
                    vertices.AddRange(bakedMesh.vertices);
                }

                var posTex = CreateBakeTexture(vertices.Count, height, anim.isLooping);

                for (var y = 0; y < height; y++)
                {
                    anim.SampleAnimation(target.gameObject, y * dt);
                    vertices = new List<Vector3>();
                    foreach (var r in renderers)
                    {
                        r.BakeMesh(bakedMesh);
                        vertices.AddRange(bakedMesh.vertices);
                    }

                    for (var x = 0; x < vertices.Count; x++)
                    {
                        posTex.SetPixel(x, y, new Color(vertices[x].x, vertices[x].y, vertices[x].z));
                    }
                }

                bakedTextures.Add(entry.Key.name, posTex);
            }

            return bakedTextures;
        }

        Texture2D CreateBakeTexture(int w, int h, bool isLooping)
        {
            return new Texture2D(Mathf.ClosestPowerOfTwo(w), Mathf.ClosestPowerOfTwo(h), TextureFormat.RGBAHalf, false)
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

        public static float Remap(float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }
    }
}
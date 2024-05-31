
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class ShaderVariantCollector : Editor
{
    private const string RES_SCENE_PATH = "Assets/Res/SingRes/Scenes";
    private const string RES_PREFAB_ITEM_PATH = "Assets/Res/SingRes/Prefab/Item";
    private const string RES_PREFAB_SCENE_PATH = "Assets/Res/SingRes/Prefab/Scenes";
    
    // 自定义的 URP Pass 标签映射
    private static readonly Dictionary<string, PassType> urpPassTypeMapping = new Dictionary<string, PassType>
    {
        { "UniversalForward", PassType.ScriptableRenderPipeline },
        { "UniversalForwardOnly", PassType.ScriptableRenderPipeline },
        { "DepthOnly", PassType.ScriptableRenderPipeline },
        { "Universal2D", PassType.ScriptableRenderPipeline },
        { "UniversalGBuffer", PassType.ScriptableRenderPipeline },
        { "DepthNormals", PassType.ScriptableRenderPipeline },
        { "SceneSelectionPass", PassType.ScriptableRenderPipeline },
        { "ShadowCaster", PassType.ShadowCaster },
        { "SHADOWCASTER", PassType.ShadowCaster },
        { "Meta", PassType.Meta },
        { "META", PassType.Meta },
        { "SRPDefaultUnlit", PassType.ScriptableRenderPipelineDefaultUnlit },
    };
    
    [MenuItem("Tools/Collect Shader Variants")]
    public static void CollectShaderVariants()
    {
        //创建ShaderVariantCollection
        ShaderVariantCollection svc = new ShaderVariantCollection();
        
        // //搜集场景中的Shader变体
        //  string[] scenePathes = _GetAllScenePathes(RES_SCENE_PATH);
        //  foreach (var scenePath in scenePathes)
        //  {
        //      Scene scene = SceneManager.GetSceneByPath(scenePath);
        //      if (!scene.isLoaded)
        //      {
        //          scene = EditorSceneManager.OpenScene(scenePath);
        //          var rootGameObjects = scene.GetRootGameObjects();
        //          List<Material> materialList = new List<Material>();
        //          foreach (var rootGameObject in rootGameObjects)
        //          {
        //             MeshRenderer[] meshRenderers = rootGameObject.GetComponentsInChildren<MeshRenderer>(true);
        //             foreach (var meshRenderer in meshRenderers)
        //             {
        //                 Material[] materials = meshRenderer.sharedMaterials;
        //                 foreach (var material in materials)
        //                 {
        //                     if (material != null)
        //                     {
        //                         materialList.Add(material);
        //                     }
        //                 }
        //             }    
        //          }
        //
        //          foreach (var mat in materialList)
        //          {
        //              _AddShaderVariants(svc, mat);
        //          }
        //
        //          EditorSceneManager.CloseScene(scene, true);
        //      }
        //  }
         
         //搜集预制体中的Shader变体
         List<string> prefabPathes = new List<string>();
         prefabPathes.AddRange(_GetAllPrefabPathes(RES_PREFAB_ITEM_PATH));
         prefabPathes.AddRange(_GetAllPrefabPathes(RES_PREFAB_SCENE_PATH));
         foreach (var prefabScene in prefabPathes)
         {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabScene);
                GameObject go = GameObject.Instantiate(prefab);
                go.SetActive(true);
                MeshRenderer[] meshRenderers = go.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var meshRenderer in meshRenderers)
                {
                    meshRenderer.enabled = true;
                    Material[] materials = meshRenderer.sharedMaterials;
                    foreach (var material in materials)
                    {
                        if (material != null)
                        {
                            _AddShaderVariants(svc, material);
                        }
                    }
                }
                
                GameObject.DestroyImmediate(go);
         }
         
         //保存ShaderVariantCollection
         AssetDatabase.CreateAsset(svc,"Assets/SVC.shadervariants");
         AssetDatabase.SaveAssets();
    }

    private static string[] _GetAllScenePathes(string rootPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene", new string[] { rootPath });
        List<string> scenePathes = new List<string>();
        foreach (var guid in guids)
        {
            scenePathes.Add(AssetDatabase.GUIDToAssetPath(guid));
        }
        
        return scenePathes.ToArray();
    }

    private static string[] _GetAllPrefabPathes(string rootPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { rootPath });
        List<string> prefabPathes = new List<string>();
        foreach (var guid in guids)
        {
            prefabPathes.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        return prefabPathes.ToArray();
    }
    
    private static void _AddShaderVariants(ShaderVariantCollection svc, Material mat)
    {
        Shader shader = mat.shader;
        string[] keywords = mat.shaderKeywords;
        Debug.LogError(shader.name);

        for (int i = 0; i < shader.passCount; i++)
        {
            string shaderTag = shader.FindPassTagValue(i, new ShaderTagId("LightMode")).name;
            PassIdentifier identifier = new PassIdentifier();
            Type myType = typeof(PassIdentifier);
            
            FieldInfo myFieldInfo = myType.GetField("m_PassIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            object asObject = identifier;
            myFieldInfo.SetValue(asObject, (uint)i);
            identifier = (PassIdentifier)asObject;

            Debug.Log(shaderTag);
            
            List<string> keywordsList = new List<string>();
            foreach (var item in ShaderUtil.GetPassKeywords(shader, identifier))
            {
                if (item.type == ShaderKeywordType.UserDefined)
                {
                    keywordsList.Add(item.name);
                }
            }

            List<string> validKeywords = new List<string>();
            foreach (var keyword0 in keywords)
            {
                foreach (var keyword1 in keywordsList)
                {
                    if (keyword0.Equals(keyword1))
                    {
                        validKeywords.Add(keyword0);
                        break;
                    }
                }   
            }
            Debug.Log(string.Join(" ", validKeywords));

            if (!urpPassTypeMapping.TryGetValue(shaderTag, out PassType passType))
            {
                passType = PassType.ScriptableRenderPipeline;
            }
            
            ShaderVariantCollection.ShaderVariant variant = new ShaderVariantCollection.ShaderVariant(shader, passType, validKeywords.ToArray());
            svc.Add(variant);   
        }
    }
}
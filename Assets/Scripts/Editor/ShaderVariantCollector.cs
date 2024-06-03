
using System;
using System.Collections.Generic;
using System.IO;
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
    private static string[] RES_PREFAB_PATHES = new string[]
    {
        "Assets/Res/SingRes/Prefab/Item",
        "Assets/Res/SingRes/Prefab/Scenes",
        "Assets/Res/SingRes/Prefab/Effects",
        "Assets/Res/SingRes/Prefab/Loading",
    };

    private static string[] RES_MATERIAL_PATHES = new string[]
    {
        "Assets/Res/CommonRes/Materials",
    };
    
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
        //清楚旧的shader变体文件
        string filePath = "Assets/Res/SVC.shadervariants";
        if (File.Exists(filePath))
        {
            AssetDatabase.DeleteAsset(filePath);
        }
        
        //记录当前所在的场景
        Scene currentScene = EditorSceneManager.GetActiveScene();
        var currentScenePath = currentScene.path;
        
        List<ShaderVariantData>  shaderVariantDatas = new List<ShaderVariantData>();
        
        //搜集场景中的Shader变体
         string[] scenePathes = _GetAllScenePathes(RES_SCENE_PATH);
         foreach (var scenePath in scenePathes)
         {
             Scene scene = SceneManager.GetSceneByPath(scenePath);
             if (!scene.isLoaded)
             {
                 scene = EditorSceneManager.OpenScene(scenePath);
                 var rootGameObjects = scene.GetRootGameObjects();
                 List<Material> materialList = new List<Material>();
                 foreach (var rootGameObject in rootGameObjects)
                 {
                    MeshRenderer[] meshRenderers = rootGameObject.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var meshRenderer in meshRenderers)
                    {
                        Material[] materials = meshRenderer.sharedMaterials;
                        foreach (var material in materials)
                        {
                            if (material != null)
                            {
                                materialList.Add(material);
                            }
                        }
                    }    
                 }
        
                 foreach (var mat in materialList)
                 {
                     _AddShaderVariants(shaderVariantDatas, mat);
                 }
        
                 EditorSceneManager.CloseScene(scene, true);
             }
         }
         
         //搜集预制体中的Shader变体
         List<string> prefabPathes = new List<string>();
         foreach (var path in RES_PREFAB_PATHES)
         {
             prefabPathes.AddRange(_GetAllPrefabPathes(path));
         }
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
                            _AddShaderVariants(shaderVariantDatas, material);
                        }
                    }
                }
                
                ParticleSystem[] particleSystems = go.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var particleSystem in particleSystems)
                {
                    ParticleSystemRenderer particleSystemRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                    Material[] materials = particleSystemRenderer.sharedMaterials;
                    foreach (var material in materials)
                    {
                        if (material != null)
                        {
                            _AddShaderVariants(shaderVariantDatas, material);
                        }
                    }
                }
                
                GameObject.DestroyImmediate(go);
         }
         
         //收集指定材质球中的Shader变体
         List<string> materialPathes = new List<string>();
         foreach (var path in RES_MATERIAL_PATHES)
         {
             materialPathes.AddRange(_GetAllMaterialPathes(path));
         }
         foreach (var materialPath in materialPathes)
         {
             Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
             _AddShaderVariants(shaderVariantDatas, mat);
         }
         
         //保存ShaderVariantCollection
         ShaderVariantCollection svc = new ShaderVariantCollection();
         foreach (var shaderVariantData in shaderVariantDatas)
         {
             // Debug.Log(shaderVariantData.Shader.name + " " + shaderVariantData.PassType + " " +
             //                string.Join(" ", shaderVariantData.Keywords));
             if (shaderVariantData.Keywords.Length == 0)
             {
                 continue;
             }
             svc.Add(new ShaderVariantCollection.ShaderVariant(shaderVariantData.Shader, shaderVariantData.PassType, shaderVariantData.Keywords));
         }

         AssetDatabase.CreateAsset(svc, filePath);
         AssetDatabase.SaveAssets();
         Debug.LogError("Collect ShaderVariant Done!");

         //开启之前的场景
         currentScene = EditorSceneManager.GetActiveScene();
         if (!currentScene.path.Equals(currentScenePath))
         {
             EditorSceneManager.OpenScene(currentScenePath);
         }

         Resources.UnloadUnusedAssets();
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

    private static string[] _GetAllMaterialPathes(string rootPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new string[] { rootPath });
        List<string> materialPathes = new List<string>();
        foreach (var guid in guids)
        {
            materialPathes.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        return materialPathes.ToArray();
    }
    
    private static void _AddShaderVariants(List<ShaderVariantData> datas, Material mat)
    {
        Shader shader = mat.shader;
        string[] keywords = mat.shaderKeywords;

        for (int i = 0; i < shader.passCount; i++)
        {
            string shaderTag = shader.FindPassTagValue(i, new ShaderTagId("LightMode")).name;
            PassIdentifier identifier = new PassIdentifier();
            Type myType = typeof(PassIdentifier);
            
            FieldInfo myFieldInfo = myType.GetField("m_PassIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            object asObject = identifier;
            myFieldInfo.SetValue(asObject, (uint)i);
            identifier = (PassIdentifier)asObject;

            // Debug.Log(shaderTag);
            
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
            // Debug.Log(string.Join(" ", validKeywords));

            if (!urpPassTypeMapping.TryGetValue(shaderTag, out PassType passType))
            {
                passType = PassType.ScriptableRenderPipeline;
            }
            
            datas.Add(new ShaderVariantData(shader, passType, validKeywords.ToArray()));
        }
    }
    
    public class ShaderVariantData
    {
        public ShaderVariantData(Shader shader, PassType passType, string[] keywords)
        {
            Shader = shader;
            PassType = passType;
            Keywords = keywords;
        }
        
        public Shader Shader;
        public PassType PassType;
        public string[] Keywords;
    }
}
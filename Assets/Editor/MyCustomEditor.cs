using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Text.RegularExpressions;
using Editor;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UIElements;

public class MyCustomEditor : EditorWindow
{
    [SerializeField] private VisualTreeAsset m_VisualTreeAsset = default;

    private Animator m_selectedAnimator = null;
    private string m_outputPath = string.Empty;
    private Shader m_selectedShader = null;
    private bool m_packAnimations = false;
    private Space m_animationSpace = Space.Self;

    [MenuItem("Window/UI Toolkit/MyCustomEditor")]
    public static void ShowExample()
    {
        MyCustomEditor wnd = GetWindow<MyCustomEditor>();
        wnd.titleContent = new GUIContent("MyCustomEditor");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        m_VisualTreeAsset.CloneTree(root);

        var objectField = root.Q<ObjectField>("AnimatorObjectField");
        objectField.objectType = typeof(Animator);
        objectField.RegisterValueChangedCallback(evt =>
        {
            Animator animator = evt.newValue as Animator;
            m_selectedAnimator = animator;
            var animations = AnimationMapBaker.GetAllAnimations(animator);
            var animationFoldout = root.Q<Foldout>("AnimationsFoldout");
            animationFoldout.Clear();
            foreach (var anim in animations)
            {
                var label = new Label(anim.name);
                label.AddToClassList("custom-foldout-content");
                animationFoldout.contentContainer.Add(label);
            }
        });

        var multipleAnimationsCheckbox = root.Q<Toggle>("PackAnimations");
        multipleAnimationsCheckbox.RegisterValueChangedCallback(evt =>
        {
            bool newValue = evt.newValue;
            m_packAnimations = newValue;
        });

        var shaderObjectField = root.Q<ObjectField>("ShaderObjectField");
        shaderObjectField.objectType = typeof(Shader);
        shaderObjectField.RegisterValueChangedCallback(evt =>
        {
            Shader shader = evt.newValue as Shader;
            m_selectedShader = shader;
        });

        var bakeButton = root.Q<Button>("BakeButton");
        bakeButton.clicked += BakeTexture;

        var outputTextField = root.Q<TextField>("FolderOutputTextfield");
        outputTextField.textEdition.placeholder = "Assets/BakedTextures/";
        outputTextField.textEdition.isReadOnly = true;

        var selectOutputFolderButton = root.Q<Button>("SelectOutputFolderButton");
        m_outputPath = outputTextField.textEdition.placeholder;
        selectOutputFolderButton.clicked += () =>
        {
            var pattern = @"\/Assets\/";
            string outputPath = EditorUtility.OpenFolderPanel("Select the output", Application.dataPath, "");
            if (Regex.Match(outputPath, pattern).Success)
            {
                outputTextField.textEdition.placeholder = outputPath.Substring(outputPath.IndexOf("Assets"));
                m_outputPath = outputTextField.textEdition.placeholder;
            }
        };

        var animationSpaceSelector = root.Q<EnumField>("AnimationSpaceEnum");
        animationSpaceSelector.RegisterValueChangedCallback(evt =>
        {
            var newValue = (Space)evt.newValue;
            m_animationSpace = newValue;
        });
    }

    void BakeTexture()
    {
        if (m_selectedAnimator == null)
            return;

        AnimationMapBaker baker = new AnimationMapBaker();
        var animations = AnimationMapBaker.GetAllAnimations(m_selectedAnimator);
        var clip = animations[0];
        var (outputTextures, normalTextures) = baker.BakeAnimationToTexture(m_selectedAnimator.gameObject, clip,
            m_packAnimations, m_animationSpace);
        int count = 0;
        foreach (var entry in outputTextures)
        {
            var texture = entry.Value;
            var folderPath = Path.Combine(m_outputPath, "Output");
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(m_outputPath, "Output");
            }

            if (!AssetDatabase.IsValidFolder(Path.Combine(folderPath, "Material")))
            {
                AssetDatabase.CreateFolder(folderPath, "Material");
            }

            var data = entry.Value;
            var animMap = new Texture2D(data.width, data.height, TextureFormat.RGBAHalf, false);
            animMap.LoadRawTextureData(data.GetRawTextureData());
            AssetDatabase.CreateAsset(data, Path.Combine(folderPath, entry.Key.name + (count) + ".asset"));
            var normalData = normalTextures[entry.Key];
            AssetDatabase.CreateAsset(normalData,
                Path.Combine(folderPath, entry.Key.name + (count++) + "_normal.asset"));

            var material = baker.GenerateMaterial(entry.Key, data, normalData, entry.Key.name, m_selectedShader,
                clip.frameRate, clip.length);
            AssetDatabase.CreateAsset(material,
                Path.Combine(Path.Combine(folderPath, "Material"), material.name + ".mat"));
        }
    }
}
using System.ComponentModel.Composition.Hosting;
using System.IO;
using Editor;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class MyCustomEditor : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    private Animator m_selectedAnimator = null;
    
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

        var objectField = root.Q<ObjectField>("ObjectField");
        objectField.objectType = typeof(Animator);
        objectField.RegisterValueChangedCallback( evt =>
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

        var bakeButton = root.Q<Button>("BakeButton");
        bakeButton.clicked += (() =>
        {
            if (m_selectedAnimator == null)
                return;
            
            AnimationMapBaker baker = new AnimationMapBaker();
            var animations = AnimationMapBaker.GetAllAnimations(m_selectedAnimator);
            var clip = animations[0];
            var outputTextures = baker.BakeAnimationToTexture(m_selectedAnimator.gameObject, clip);
            foreach (var entry in outputTextures)
            {
                var texture = entry.Value;
                var folderPath = Path.Combine("Assets/BakedTextures/", "Output");
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    AssetDatabase.CreateFolder("Assets/BakedTextures/", "Output");
                }
                
                var data = entry.Value;
                var animMap = new Texture2D(data.width, data.height, TextureFormat.RGBAHalf, false);
                animMap.LoadRawTextureData(data.GetRawTextureData());
                AssetDatabase.CreateAsset(data, Path.Combine(folderPath, entry.Key + ".asset"));
            }
            
            
        });
    }
}

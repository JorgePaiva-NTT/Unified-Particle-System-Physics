using UnityEngine;
using UnityEditor;

using Assets.Editor;
using System;

//[CustomEditor(typeof(GPUParticleSystem))]
[CanEditMultipleObjects]
public class ParticleSystemEditor : Editor {

	private const string WarningPerformanceImpact = "Changing this has Impact on Performance.\nA mesh with a high number of triangles has negative impact on performance";

	private GUIStyle SeparatorStyle;
	private GUIStyle VerticalLayoutStyle;

	private bool GlobalPropertiesFoldout = false;
	private bool SystemPropertiesFoldout = false;
	private bool ParticlePropertiesFoldout = false;
	private bool CollisionlPropertiesFoldout = false;


	private Font modernFont;
	private GUIContent maxColorContent;
	private GUIContent minColorContent;

    private SerializedProperty NumParticles;
	private SerializedProperty Gravity;
	private SerializedProperty Boundaries;

    private SerializedProperty particleRadius;
	private SerializedProperty collideSpring;
	private SerializedProperty collideDamping;
	private SerializedProperty collideShear;
	private SerializedProperty collideAttraction;
	private SerializedProperty globalDamping;
    private SerializedProperty boundaryDamping;

    private SerializedProperty Mesh;
	private SerializedProperty ComputeShader;
    private SerializedProperty DebugMaterial;
    private SerializedProperty velocitiesDebugMaterial;
    private SerializedProperty Material;

    private SerializedProperty minColor;
	private SerializedProperty maxColor;

    private SerializedProperty unityTimeStep;
    private SerializedProperty customTimeStep;
    private SerializedProperty synchronize;
    private SerializedProperty showGrid;
    private SerializedProperty sphere;

    Texture2D CreateColoredHeader(Color color) {
		Texture2D header = new Texture2D(100, 20);
		var pixels = header.GetPixels32();
		for (int r = 0; r < pixels.Length; r++) {
			pixels[r] = color;
		}
		header.SetPixels32(pixels);
		header.Apply(true);
		return header;
	}

	void OnEnable() {
		Gravity = serializedObject.FindProperty("Gravity");
		Boundaries = serializedObject.FindProperty("Boundaries");
		particleRadius = serializedObject.FindProperty("particleRadius");
		collideSpring = serializedObject.FindProperty("collideSpring");
		collideDamping = serializedObject.FindProperty("collideDamping");
		collideShear = serializedObject.FindProperty("collideShear");
		collideAttraction = serializedObject.FindProperty("collideAttraction");
		globalDamping = serializedObject.FindProperty("globalDamping");
		NumParticles = serializedObject.FindProperty("NumParticles");
        boundaryDamping = serializedObject.FindProperty("boundaryDamping");


        Mesh = serializedObject.FindProperty("mesh");
		ComputeShader = serializedObject.FindProperty("computeShader");
	    DebugMaterial = serializedObject.FindProperty("positionsDebugMaterial");
	    velocitiesDebugMaterial = serializedObject.FindProperty("velocitiesDebugMaterial");
        Material = serializedObject.FindProperty("mat");

		minColor = serializedObject.FindProperty("minColor");
		maxColor = serializedObject.FindProperty("maxColor");

        unityTimeStep = serializedObject.FindProperty("unityTimeStep");
        customTimeStep = serializedObject.FindProperty("customTimeStep");
        synchronize = serializedObject.FindProperty("synchronizeTime");
        showGrid = serializedObject.FindProperty("ShowGrid");
        sphere = serializedObject.FindProperty("sphere");

        modernFont = Resources.Load<Font>("Roboto-Bold");
		
		maxColorContent = new GUIContent("Max Color", "Particle color given a certain velocity");
		minColorContent = new GUIContent("Min Color", "Particle color given a certain velocity");

		SeparatorStyle = new GUIStyle
		{
			richText = true,
			fontStyle = FontStyle.Bold,
			normal = new GUIStyleState()
			{
				textColor = Color.black,
				background = CreateColoredHeader(Color.yellow)
			}
		};

		VerticalLayoutStyle = new GUIStyle
		{   
			normal = new GUIStyleState
			{
				textColor = Color.red,
				background = CreateColoredHeader(Color.grey)
                
			}
		};
	}

	public override void OnInspectorGUI() {
        serializedObject.Update();

		GUI.skin.font = modernFont;
        var vertical =  EditorGUILayout.BeginVertical(VerticalLayoutStyle);
		{
			BeginSeparate("Initialization Properties");
                CustomFloatField("Unity Current Time Step", unityTimeStep);
                if(!CustomToggle("Synchronize Time Step", synchronize)) {
                    CustomFloatField("Custom Time Step", customTimeStep);
                }
                if (CustomToggle("Show Grid", showGrid)) {
                    string message = 
                                 "Grid being shown using GIZMOS\n" +
                                 "Green->Only 1 Particle in the Cell\n" +
                                 "Yellow -> 2 or 3  Particles in the Cell\n" +
                                 "Red    -> 4 or more Particles in the Cell";
                    EditorGUILayout.HelpBox(message, MessageType.Warning, false);    
                }
            EndSeparate();


			BeginSeparate("Global Properties");
			//GlobalPropertiesFoldout = EditorGUILayout.Foldout(GlobalPropertiesFoldout, "HIDE");
			//if (GlobalPropertiesFoldout) 
			//{
				CustomSlider("Global Damping", globalDamping, 0.9f, 1.0f);
                CustomSlider("Boundary Damping", boundaryDamping, 0.001f, 1.0f);
			//}
			EndSeparate(); //GLOBAL PROPERTIES

			BeginSeparate("System Properties");
			//SystemPropertiesFoldout = EditorGUILayout.Foldout(SystemPropertiesFoldout, "HIDE");
			//if (SystemPropertiesFoldout) 
			//{
				CustomSlider("World Gravity", Gravity, -10.0f, 10.0f);
                CustomVector3("Boundaries Size", Boundaries);
				GUILayout.Space(5);
				CustomIntField("Particle Number", NumParticles);
				GUILayout.Space(15);
				EditorGUILayout.BeginVertical();
				{
					CustomObjectField("Compute Shader", ComputeShader, typeof(ComputeShader));
				    CustomObjectField("Debug Positions material", DebugMaterial, typeof(Material));
				    CustomObjectField("Debug Velocities material", velocitiesDebugMaterial, typeof(Material));
                    CustomObjectField("Particles Material", Material, typeof(Material));
					EditorGUILayout.Space();
					EditorGUILayout.HelpBox(WarningPerformanceImpact, MessageType.Warning, false);
					CustomObjectField("Particles mesh", Mesh, typeof(Mesh));
                    CustomObjectField("Rigid Body", sphere, typeof(Transform));
                }
				EditorGUILayout.EndVertical();
			//}
			EndSeparate();

			BeginSeparate("Particle Properties");
			//ParticlePropertiesFoldout = EditorGUILayout.Foldout(ParticlePropertiesFoldout, "HIDE");
			//if (ParticlePropertiesFoldout) 
			//{
					CustomSlider("Particle Radius", particleRadius, 0.1f, 5.0f);
					CustomColorField();
			//}
			EndSeparate();

			BeginSeparate("Collision Properties");
			//CollisionlPropertiesFoldout = EditorGUILayout.Foldout(CollisionlPropertiesFoldout, "HIDE");
			//if (CollisionlPropertiesFoldout) 
			//{
					CustomSlider("Collision Spring", collideSpring, 0.1f, 10.0f);
					CustomSlider("Collision Damping", collideDamping, 0.001f, 0.1f);
					CustomSlider("Collision Shear", collideShear, 0.001f, 0.1f);
					CustomSlider("Collision Attraction", collideAttraction, 0.001f, 0.1f);
			//}
			EndSeparate();
		}
		EditorGUILayout.EndVertical();

		serializedObject.ApplyModifiedProperties();
	}

	private void CustomColorField() 
	{
		EditorGUILayout.BeginHorizontal();
		{
			CustomLabel("Min Color");
			//minColor.colorValue = EditorGUILayout.ColorField(new GUIContent(), minColor.colorValue, true, true, true, null);
		}
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.BeginHorizontal();
		{
			CustomLabel("Max Color");
			//maxColor.colorValue = EditorGUILayout.ColorField(new GUIContent(), maxColor.colorValue, true, true, true, null);
		}
		EditorGUILayout.EndHorizontal();
	}

    private void CustomVector3(string label, SerializedProperty property)
    {
        EditorGUILayout.BeginHorizontal();
        {
            CustomLabel(label);
            property.vector3Value = EditorGUILayout.Vector3Field("", property.vector3Value);
        }
        EditorGUILayout.EndHorizontal();
    }

	private void CustomLabel(string label) {
		GUILayout.Label(label, "ChannelStripAttenuationMarkerSquare", GUILayout.Width(200));
	}

	private void CustomSlider(string label, SerializedProperty property, float min,  float max) {
		EditorGUILayout.BeginHorizontal();
		{
			CustomLabel(label);
			property.floatValue = GUILayout.HorizontalSlider(property.floatValue,
																  min,
																  max,
																  "PreSlider",  //Slider skin
																  "PreSliderThumb"); //Thumb skin
			property.floatValue = EditorGUILayout.FloatField(property.floatValue, GUILayout.Width(65));
			GUILayout.Space(5);
		}
		EditorGUILayout.EndHorizontal();
	}

	private void CustomIntField(string label, SerializedProperty property) {
		EditorGUILayout.BeginHorizontal();
		{
			CustomLabel(label);
			property.intValue = EditorGUILayout.IntField(property.intValue, "ObjectFieldThumb");
		}
		EditorGUILayout.EndHorizontal();
	}

    private void CustomFloatField(string label, SerializedProperty property)
    {
        EditorGUILayout.BeginHorizontal();
        {
            CustomLabel(label);
            property.floatValue = EditorGUILayout.FloatField(property.floatValue, "ObjectFieldThumb");
        }
        EditorGUILayout.EndHorizontal();
    }

    private bool CustomToggle(string label, SerializedProperty property)
    {
        EditorGUILayout.BeginHorizontal();
        {
            CustomLabel(label);
            property.boolValue = EditorGUILayout.Toggle(property.boolValue);
        }
        EditorGUILayout.EndHorizontal();
        return property.boolValue;
    }

    private void CustomObjectField(string label, SerializedProperty property, Type objType) {
		EditorGUILayout.BeginHorizontal();
		{
			CustomLabel(label);
			property.objectReferenceValue = EditorGUILayout.ObjectField(property.objectReferenceValue, objType, true);
		}
		EditorGUILayout.EndHorizontal();
	}

	private void BeginSeparate() {

		EditorGUILayout.BeginVertical();

		GUILayout.Space(5);
		EditorGUILayout.Separator();
		//EditorGUI.indentLevel += 1;
	}

	private void BeginSeparate(string label) {

		EditorGUILayout.BeginVertical();

		GUILayout.Space(5);
		EditorGUILayout.Separator();
		GUILayout.Label(label, "WarningOverlay");
		//EditorGUI.indentLevel += 1;
	}

	private void EndSeparate() {
		//EditorGUI.indentLevel -= 1;

		EditorGUILayout.EndVertical();
	}
}
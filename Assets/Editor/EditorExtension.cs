using UnityEditor;

namespace Assets.Editor
{
    static class EditorExtension {
		public static T TypedField<T>(string label, T obj) where T : UnityEngine.Object {
			return EditorGUILayout.ObjectField(label, obj, typeof(T), false) as T;
		}
	}
}

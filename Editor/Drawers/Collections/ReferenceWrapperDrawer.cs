using Fusumity.Collections;
using Fusumity.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace Fusumity.Editor.Drawers.Specific.Collections
{
	[CustomPropertyDrawer(typeof(ReferenceWrapper<,>))]
	public class ReferenceWrapperDrawerDrawer : SerializeReferenceSelectorAttributeDrawer
	{
		private const string _valueName = "_value";

		public override void ModifyPropertyData()
		{
			base.ModifyPropertyData();

			var property = propertyData.property;
			var valueProperty = property.FindPropertyRelative(_valueName);

			propertyData.bodyHeight = valueProperty.GetBodyHeight() - EditorGUIUtility.singleLineHeight;
		}

		public override void DrawSubBody(Rect position)
		{
			var property = propertyData.property;
			var fieldType = fieldInfo.FieldType.IsArray ? fieldInfo.FieldType.GetElementType() : fieldInfo.FieldType;

			var valueType = fieldType.GetGenericArguments()[1];

			var targetType = valueType;
			var currentType = property.GetPropertyTypeByLocalPath(_valueName);

			SelectType(position, currentType, targetType, true);
		}

		protected override void SetValue(SerializedProperty property, object value)
		{
			base.SetValue(property.FindPropertyRelative(_valueName), value);
		}

		public override void DrawBody(Rect position)
		{
			var property = propertyData.property;
			var valueProperty = property.FindPropertyRelative(_valueName);

			valueProperty.DrawBody(position);
		}
	}
}

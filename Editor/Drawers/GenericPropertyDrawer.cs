using System;
using System.Collections.Generic;
using System.Reflection;
using Fusumity.Editor.Utilities;
using Fusumity.Attributes;
using UnityEditor;
using UnityEngine;

namespace Fusumity.Editor.Drawers
{
	[CustomPropertyDrawer(typeof(GenericDrawerAttribute))]
	public class GenericPropertyDrawer : PropertyDrawer
	{
		private const float indentWidth = 15f;

		private static readonly Type _baseDrawerType = typeof(GenericPropertyDrawer);
		private static readonly Type _attributeType = typeof(GenericDrawerAttribute);

		private static Dictionary<Type, Type> _attributeTypeToDrawerType;

		private GenericDrawerAttribute[] _genericAttributes;
		private GenericPropertyDrawer[] _genericDrawers;

		protected PropertyData propertyData;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			LazyInitializeAttributes();
			LazyInitializeDrawers();
			InitializePropertyData(property, label);

			return propertyData.GetTotalHeight();
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (!propertyData.drawProperty)
				return;

			GUI.enabled = propertyData.isEnabled;
			var lastIndentLevel = EditorGUI.indentLevel;
			var lastLabelWidth = EditorGUIUtility.labelWidth;

			EditorGUI.BeginChangeCheck();

			var propertyPosition = position;
			if (propertyData.hasBeforeExtension)
				propertyPosition.yMin += propertyData.beforeExtensionHeight;
			if (propertyData.hasAfterExtension)
				propertyPosition.yMax -= propertyData.afterExtensionHeight;

			var beforeExtensionPosition = propertyData.hasBeforeExtension
				? new Rect(position.x, position.y, position.width, propertyData.beforeExtensionHeight)
				: Rect.zero;

			var labelPosition = propertyData.hasLabel
				? new Rect(propertyPosition.x, propertyPosition.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight)
				: Rect.zero;
			var foldoutPosition = new Rect(propertyPosition.x, propertyPosition.y, propertyPosition.width, EditorGUIUtility.singleLineHeight);
			var subBodyPosition = propertyData.hasLabel & !propertyData.labelIntersectSubBody
				? new Rect(propertyPosition.x + EditorGUIUtility.labelWidth, propertyPosition.y,
					propertyPosition.width - EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight)
				: new Rect(propertyPosition.x, propertyPosition.y, propertyPosition.width, EditorGUIUtility.singleLineHeight);
			var bodyPosition = (propertyData.hasLabel | propertyData.hasSubBody)
				? new Rect(propertyPosition.x, propertyPosition.y + EditorGUIUtility.singleLineHeight, propertyPosition.width,
					propertyPosition.height - EditorGUIUtility.singleLineHeight)
				: propertyPosition;

			var afterExtensionPosition = propertyData.hasAfterExtension
				? new Rect(position.x, propertyPosition.yMax, position.width, propertyData.afterExtensionHeight)
				: Rect.zero;

			ExecuteValidateBeforeDrawing();

			if (propertyData.hasBeforeExtension)
			{
				ExecuteDrawBeforeExtension(beforeExtensionPosition);
			}

			if (propertyData.hasLabel)
			{
				ExecuteDrawLabel(labelPosition);
			}

			if (propertyData.hasLabel)
			{
				ExecuteDrawLabel(labelPosition);
			}

			if (propertyData.hasFoldout)
			{
				propertyData.property.isExpanded = EditorGUI.Foldout(foldoutPosition, property.isExpanded, "");
			}

			if (propertyData.hasSubBody & (propertyData.property.isExpanded | !propertyData.hasFoldout | propertyData.drawSubBodyWhenRollUp))
			{
				if (!propertyData.hasLabel | !propertyData.labelIntersectSubBody)
				{
					EditorGUIUtility.labelWidth = indentWidth;
				}

				ExecuteDrawSubBody(subBodyPosition);

				EditorGUIUtility.labelWidth = lastLabelWidth;
			}

			if (propertyData.hasBody & (propertyData.property.isExpanded | !propertyData.hasFoldout))
			{
				if (propertyData.hasLabel)
				{
					EditorGUI.indentLevel++;
				}

				ExecuteDrawBody(bodyPosition);
			}

			if (propertyData.hasAfterExtension)
			{
				ExecuteDrawAfterExtension(afterExtensionPosition);
			}

			if (EditorGUI.EndChangeCheck())
			{
				ExecuteOnPropertyChanged();
			}

			EditorGUIUtility.labelWidth = lastLabelWidth;
			EditorGUI.indentLevel = lastIndentLevel;
			GUI.enabled = true;
		}

		#region Initialization

		private void LazyInitializeAttributes()
		{
			if (_genericAttributes != null)
				return;

			var attributes = new List<GenericDrawerAttribute>();
			var customAttributes = fieldInfo.GetCustomAttributes();

			foreach (var customAttribute in customAttributes)
			{
				if (!(customAttribute is GenericDrawerAttribute genericAttribute))
					continue;
				if (genericAttribute.Equals(attribute))
					continue;
				attributes.Add(genericAttribute);
			}

			_genericAttributes = attributes.ToArray();
		}

		private void LazyInitializeDrawers()
		{
			if (_genericDrawers != null)
				return;

			if (_attributeTypeToDrawerType == null)
			{
				var drawersTypes = _baseDrawerType.GetInheritorTypes();
				_attributeTypeToDrawerType = new Dictionary<Type, Type>(drawersTypes.Length * 3);

				foreach (var drawerType in drawersTypes)
				{
					var customAttributes = drawerType.GetCustomAttributes<CustomPropertyDrawer>();

					foreach (var customAttribute in customAttributes)
					{
						var customAttributeTypes = customAttribute.GetCustomPropertyDrawerTypes();
						foreach (var customAttributeType in customAttributeTypes)
						{
							_attributeTypeToDrawerType.Add(customAttributeType, drawerType);
						}
					}
				}
			}

			_genericDrawers = new GenericPropertyDrawer[_genericAttributes.Length];

			for (var i = 0; i < _genericAttributes.Length; i++)
			{
				var genericAttribute = _genericAttributes[i];

				if (!_attributeTypeToDrawerType.TryGetValue(genericAttribute.GetType(), out var drawerType))
				{
					drawerType = _baseDrawerType;
				}

				var drawer = (GenericPropertyDrawer)Activator.CreateInstance(drawerType);
				drawer.SetAttribute(genericAttribute);
				drawer.SetFieldInfo(fieldInfo);

				_genericDrawers[i] = drawer;
			}
		}

		private void InitializePropertyData(SerializedProperty property, GUIContent label)
		{
			if (propertyData == null)
			{
				propertyData = new PropertyData();

				foreach (var drawer in _genericDrawers)
				{
					drawer.propertyData = propertyData;
				}
			}

			propertyData.ResetData(property, label);
			ExecuteModifyPropertyData();
		}

		#endregion

		#region Custom Executers

		private void ExecuteModifyPropertyData()
		{
			ModifyPropertyData();

			foreach (var drawer in _genericDrawers)
			{
				drawer.ModifyPropertyData();
			}
		}

		private void ExecuteValidateBeforeDrawing()
		{
			ValidateBeforeDrawing();

			foreach (var drawer in _genericDrawers)
			{
				drawer.ValidateBeforeDrawing();
			}
		}

		private void ExecuteDrawBeforeExtension(Rect position)
		{
			DrawBeforeExtension(ref position);
			foreach (var drawer in _genericDrawers)
			{
				drawer.DrawBeforeExtension(ref position);
			}
		}

		private void ExecuteDrawLabel(Rect position)
		{
			if (!OverrideLabelDrawing())
			{
				foreach (var drawer in _genericDrawers)
				{
					if (drawer.OverrideLabelDrawing())
					{
						drawer.DrawLabel(position);
						return;
					}
				}
			}

			DrawLabel(position);
		}

		private void ExecuteDrawSubBody(Rect position)
		{
			if (!OverrideSubBodyDrawing())
			{
				foreach (var drawer in _genericDrawers)
				{
					if (drawer.OverrideSubBodyDrawing())
					{
						drawer.DrawSubBody(position);
						return;
					}
				}
			}

			DrawSubBody(position);
		}

		private void ExecuteDrawBody(Rect position)
		{
			if (!OverrideBodyDrawing())
			{
				foreach (var drawer in _genericDrawers)
				{
					if (drawer.OverrideBodyDrawing())
					{
						drawer.DrawBody(position);
						return;
					}
				}
			}

			DrawBody(position);
		}

		private void ExecuteDrawAfterExtension(Rect position)
		{
			DrawAfterExtension(ref position);
			foreach (var drawer in _genericDrawers)
			{
				drawer.DrawAfterExtension(ref position);
			}
		}

		private void ExecuteOnPropertyChanged()
		{
			OnPropertyChanged();

			foreach (var drawer in _genericDrawers)
			{
				drawer.OnPropertyChanged();
			}
		}

		#endregion

		#region Custom

		public virtual void ModifyPropertyData() {}

		public virtual void ValidateBeforeDrawing() {}

		public virtual bool OverrideLabelDrawing() => false;

		public virtual void DrawBeforeExtension(ref Rect position) {}

		public virtual void DrawLabel(Rect position)
		{
			EditorGUI.LabelField(position, propertyData.label, propertyData.labelStyle);
		}

		public virtual bool OverrideSubBodyDrawing() => false;

		public virtual void DrawSubBody(Rect position)
		{
			propertyData.property.DrawBody(position);
		}

		public virtual bool OverrideBodyDrawing() => false;

		public virtual void DrawBody(Rect position)
		{
			propertyData.property.DrawBody(position);
		}

		public virtual void DrawAfterExtension(ref Rect position) {}

		public virtual void OnPropertyChanged() {}

		#endregion
	}

	public class PropertyData
	{
		public SerializedProperty property;
		public GUIContent label;

		public bool drawProperty;
		public bool isEnabled;

		public bool hasBeforeExtension;
		public bool hasFoldout;
		public bool hasLabel;
		public bool hasSubBody;
		public bool hasBody;
		public bool hasAfterExtension;

		public bool drawSubBodyWhenRollUp;
		public bool labelIntersectSubBody;

		public float beforeExtensionHeight;
		public float labelHeight;
		public float bodyHeight;
		public float afterExtensionHeight;
		public GUIStyle labelStyle;

		public void ResetData(SerializedProperty property, GUIContent label)
		{
			this.property = property;
			this.label = label;

			drawProperty = true;
			isEnabled = true;

			hasBeforeExtension = false;
			hasFoldout = property.hasChildren;
			hasLabel = true;
			hasSubBody = !property.hasChildren;
			hasBody = property.hasChildren & property.isExpanded;
			hasAfterExtension = false;

			drawSubBodyWhenRollUp = true;
			labelIntersectSubBody = true;

			beforeExtensionHeight = 0f;
			labelHeight = EditorGUIUtility.singleLineHeight;
			bodyHeight = EditorGUI.GetPropertyHeight(property, true);
			afterExtensionHeight = 0f;
			if (property.hasChildren)
				bodyHeight -= labelHeight;

			labelStyle = EditorStyles.label;
		}

		public float GetTotalHeight()
		{
			var height = 0f;
			if (!drawProperty)
				return height;

			if (hasBeforeExtension)
			{
				height += beforeExtensionHeight;
			}
			if (hasLabel | hasSubBody | (!hasBody & hasFoldout))
			{
				height += labelHeight;
			}
			if (hasBody)
			{
				height += bodyHeight;
			}
			if (hasAfterExtension)
			{
				height += afterExtensionHeight;
			}

			return height;
		}
	}
}

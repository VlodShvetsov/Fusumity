using System;
using System.Collections.Generic;
using System.Reflection;
using Fusumity.Attributes;
using Fusumity.Editor.Extensions;
using UnityEditor;
using UnityEngine;

namespace Fusumity.Editor.Drawers
{
	[CustomPropertyDrawer(typeof(FusumityDrawerAttribute))]
	[CustomPropertyDrawer(typeof(IFusumitySerializable), true)]
	public class FusumityPropertyDrawer : PropertyDrawer
	{
		public static readonly GUIContent SUB_BODY_GUI_CONTENT = new (" ");

		private static readonly Type BASE_DRAWER_TYPE = typeof(FusumityPropertyDrawer);
		private static readonly Type ATTRIBUTE_TYPE = typeof(FusumityDrawerAttribute);

		private static Dictionary<Type, Type> _typeToDrawerType;
		private static HashSet<string> _currentPropertyPath = new HashSet<string>();

		private List<FusumityDrawerAttribute> _fusumityAttributes;
		private List<FusumityPropertyDrawer> _fusumityDrawers;
		// If not use this arrays will suck (very long story).
		private Dictionary<string, PropertyData> _pathToPropertyData;

		public PropertyData currentPropertyData;

		public virtual bool OverrideMethods => true;

		public T GetPersistentData<T>()
		{
			return currentPropertyData.GetPersistentData<T>(this);
		}

		public void SetPersistentData<T>(T value)
		{
			currentPropertyData.SetPersistentData(this, value);
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var propertyPath = property.propertyPath;
			var parentPropertyPath = property.GetParentPropertyPath();

			if (_currentPropertyPath.Contains(propertyPath))
			{
				// When you open the unity Object selection field the _currentPropertyPath will not cleaned in that frame :(
				if (_currentPropertyPath.Contains(parentPropertyPath))
				{
					var height = property.GetPropertyHeight_Cached();
					_currentPropertyPath.Clear();
					return height;
				}
				_currentPropertyPath.Clear();
			}
			else if (!_currentPropertyPath.Contains(parentPropertyPath))
			{
				_currentPropertyPath.Add(parentPropertyPath);
			}

			_currentPropertyPath.Add(propertyPath);

			LazyInitializeAttributes();
			LazyInitializeDrawers(property);
			LazyInitializePropertyData(propertyPath);
			SetupPropertyData(propertyPath);

			currentPropertyData.ResetData(property, label);
			ExecuteModifyPropertyData();

			_currentPropertyPath.Remove(propertyPath);
			_currentPropertyPath.Remove(parentPropertyPath);

			return currentPropertyData.GetTotalHeight();
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (position == new Rect(0, 0, 1, 1))
				return;
			var propertyPath = property.propertyPath;
			if (_currentPropertyPath.Contains(propertyPath))
			{
				property.PropertyField_Cached();
				return;
			}

			SetupPropertyData(propertyPath);

			if (currentPropertyData == null || !currentPropertyData.drawProperty || currentPropertyData.forceBreak)
				return;

			_currentPropertyPath.Add(propertyPath);

			var oldBackgroundColor = GUI.backgroundColor;
			var oldGuiEnabled = GUI.enabled;
			var lastIndentLevel = EditorGUI.indentLevel;
			var lastLabelWidth = EditorGUIUtility.labelWidth;

			GUI.backgroundColor = currentPropertyData.backgroundColor;
			switch (currentPropertyData.enableState)
			{
				case EnableState.ForceDisable:
				case EnableState.Disable:
					GUI.enabled = false;
					break;
				case EnableState.ForceEnable:
					GUI.enabled = true;
					break;
			}

			EditorGUI.BeginChangeCheck();
			EditorGUI.BeginProperty(position, label, currentPropertyData.property);

			EditorGUI.indentLevel += currentPropertyData.indent;

			position.yMin += currentPropertyData.drawOffsetY;
			position.xMin += currentPropertyData.drawOffsetX;

			var propertyPosition = position;
			if (currentPropertyData.hasBeforeExtension)
				propertyPosition.yMin += currentPropertyData.beforeExtensionHeight;
			if (currentPropertyData.hasAfterExtension)
				propertyPosition.yMax -= currentPropertyData.afterExtensionHeight;

			var foldoutPosition = new Rect(propertyPosition.x, propertyPosition.y, 0f, EditorGUIUtility.singleLineHeight);
			if (currentPropertyData.hasFoldout)
			{
				//if (currentPropertyData.isArrayElement)
					//propertyPosition.xMin += EditorExt.ARRAY_BLIND_WIDTH;
				foldoutPosition.x = propertyPosition.x;
				foldoutPosition.width = EditorExt.FOLDOUT_WIDTH;
			}

			var beforeExtensionPosition = new Rect(position.x, position.y, 0f, 0f);
			if (currentPropertyData.hasBeforeExtension)
			{
				beforeExtensionPosition.width = position.width;
				beforeExtensionPosition.height = currentPropertyData.beforeExtensionHeight;
			}

			var prefixPosition = new Rect(foldoutPosition.xMax, propertyPosition.y, 0f, EditorGUIUtility.singleLineHeight);
			if (currentPropertyData.ShouldDrawLabelPrefix())
			{
				var prefixWidth = currentPropertyData.labelPrefixWidth;
				if (prefixWidth > EditorGUIUtility.labelWidth)
					prefixWidth = EditorGUIUtility.labelWidth;

				prefixPosition.width = prefixWidth;
			}

			var labelPosition = new Rect(prefixPosition.xMax, propertyPosition.y, 0f, EditorGUIUtility.singleLineHeight);
			if (currentPropertyData.hasLabel)
			{
				var labelWidth = EditorGUIUtility.labelWidth - prefixPosition.width;
				labelPosition.width = labelWidth;
			}

			var subBodyPosition = new Rect(labelPosition.xMin, propertyPosition.y,
				propertyPosition.width - prefixPosition.width - foldoutPosition.width,
				EditorGUIUtility.singleLineHeight);
			var bodyPosition = (currentPropertyData.hasLabel | currentPropertyData.hasSubBody)
				? new Rect(propertyPosition.x, propertyPosition.y + EditorGUIUtility.singleLineHeight, propertyPosition.width, propertyPosition.height - EditorGUIUtility.singleLineHeight)
				: new Rect(labelPosition.xMax, propertyPosition.y, propertyPosition.width, propertyPosition.height);

			var afterExtensionPosition = currentPropertyData.hasAfterExtension
				? new Rect(position.x, propertyPosition.yMax, position.width, currentPropertyData.afterExtensionHeight)
				: Rect.zero;

			ExecuteValidateBeforeDrawing();

			if (currentPropertyData.hasBeforeExtension)
			{
				ExecuteDrawBeforeExtension(beforeExtensionPosition);
			}

			if (currentPropertyData.ShouldDrawLabelPrefix())
			{
				var isEnabled = GUI.enabled;
				if (currentPropertyData.hasLabel)
					GUI.enabled = false;

				DrawLabelPrefix(prefixPosition);

				if (currentPropertyData.hasLabel)
					GUI.enabled = isEnabled;
			}

			if (currentPropertyData.hasLabel)
			{
				ExecuteDrawLabel(labelPosition);
			}

			if (currentPropertyData.hasFoldout)
			{
				foldoutPosition.xMax = labelPosition.xMax;
				EditorGUI.indentLevel += currentPropertyData.foldoutIndent;
				currentPropertyData.property.isExpanded = EditorGUI.Foldout(foldoutPosition, currentPropertyData.property.isExpanded, "", toggleOnLabelClick: true);
				EditorGUI.indentLevel -= currentPropertyData.foldoutIndent;
			}

			if (currentPropertyData.ShouldDrawSubBody())
			{
				EditorGUIUtility.labelWidth = labelPosition.width == 0 ? 0.1f : labelPosition.width;
				ExecuteDrawSubBody(subBodyPosition);
				EditorGUIUtility.labelWidth = lastLabelWidth;
			}

			if (currentPropertyData.ShouldDrawBody())
			{
				if (currentPropertyData.hasLabel)
				{
					EditorGUI.indentLevel++;
				}

				ExecuteDrawBody(bodyPosition);
			}

			if (!currentPropertyData.forceBreak && currentPropertyData.hasAfterExtension)
			{
				ExecuteDrawAfterExtension(afterExtensionPosition);
			}

			EditorGUI.EndProperty();
			if (!currentPropertyData.forceBreak && EditorGUI.EndChangeCheck())
			{
				property.serializedObject.ApplyModifiedProperties();
				ExecuteOnPropertyChanged();
			}

			EditorGUIUtility.labelWidth = lastLabelWidth;
			EditorGUI.indentLevel = lastIndentLevel;
			GUI.enabled = oldGuiEnabled;
			GUI.backgroundColor = oldBackgroundColor;

			_currentPropertyPath.Remove(propertyPath);
		}

		#region Initialization

		private void LazyInitializeAttributes()
		{
			if (_fusumityAttributes != null && _fusumityAttributes.Count > 0)
				return;

			var attributes = new List<FusumityDrawerAttribute>();
			var customAttributes = fieldInfo.GetCustomAttributes();

			foreach (var customAttribute in customAttributes)
			{
				if (!(customAttribute is FusumityDrawerAttribute fusumityDrawerAttribute))
					continue;
				if (fusumityDrawerAttribute.Equals(attribute))
					continue;
				attributes.Add(fusumityDrawerAttribute);
			}

			_fusumityAttributes = attributes;
		}

		private void LazyInitializeDrawers(SerializedProperty property)
		{
			if (_fusumityDrawers != null && _fusumityDrawers.Count > 0)
				return;

			if (_typeToDrawerType == null)
			{
				var drawersTypes = BASE_DRAWER_TYPE.GetInheritorTypes();
				_typeToDrawerType = new Dictionary<Type, Type>(drawersTypes.Length * 3);

				foreach (var drawerType in drawersTypes)
				{
					var customAttributes = drawerType.GetCustomAttributes<CustomPropertyDrawer>();

					foreach (var customAttribute in customAttributes)
					{
						var customAttributeTypes = customAttribute.GetCustomPropertyDrawerTypes();
						foreach (var customAttributeType in customAttributeTypes)
						{
							_typeToDrawerType.TryAdd(customAttributeType, drawerType);
						}
					}
				}
			}

			_fusumityDrawers = new List<FusumityPropertyDrawer>(_fusumityAttributes.Count);
			{
				var propertyType = property.GetPropertyType();
				if (propertyType != null)
				{
					if (propertyType.IsGenericType)
						propertyType = propertyType.GetGenericTypeDefinition();
					if (_typeToDrawerType.TryGetValue(propertyType, out var drawerType) && GetType() != drawerType)
					{
						var drawer = (FusumityPropertyDrawer)Activator.CreateInstance(drawerType);
						drawer.SetFieldInfo(fieldInfo);

						_fusumityDrawers.Add(drawer);
					}
				}
			}

			for (var i = 0; i < _fusumityAttributes.Count; i++)
			{
				var genericAttribute = _fusumityAttributes[i];

				if (!_typeToDrawerType.TryGetValue(genericAttribute.GetType(), out var drawerType))
				{
					drawerType = BASE_DRAWER_TYPE;
				}

				var drawer = (FusumityPropertyDrawer)Activator.CreateInstance(drawerType);
				drawer.SetAttribute(genericAttribute);
				drawer.SetFieldInfo(fieldInfo);

				_fusumityDrawers.Add(drawer);
			}
		}

		private void LazyInitializePropertyData(string propertyPath)
		{
			if (_pathToPropertyData == null)
				_pathToPropertyData = new Dictionary<string, PropertyData>();
			if (!_pathToPropertyData.TryGetValue(propertyPath, out var drawerData))
			{
				drawerData = new PropertyData();
				currentPropertyData = new PropertyData();
				_pathToPropertyData.Add(propertyPath, drawerData);
			}
		}

		private void SetupPropertyData(string propertyPath)
		{
			if (_pathToPropertyData == null || !_pathToPropertyData.TryGetValue(propertyPath, out currentPropertyData))
				return;

			foreach (var drawer in _fusumityDrawers)
			{
				drawer.currentPropertyData = currentPropertyData;
			}
		}

		#endregion

		#region Custom Executers

		private void ExecuteModifyPropertyData()
		{
			ModifyPropertyData();

			foreach (var drawer in _fusumityDrawers)
			{
				drawer.ModifyPropertyData();
			}
		}

		private void ExecuteValidateBeforeDrawing()
		{
			ValidateBeforeDrawing();

			foreach (var drawer in _fusumityDrawers)
			{
				drawer.ValidateBeforeDrawing();
			}
		}

		private void ExecuteDrawBeforeExtension(Rect position)
		{
			DrawBeforeExtension(ref position);
			foreach (var drawer in _fusumityDrawers)
			{
				drawer.DrawBeforeExtension(ref position);
			}
		}

		private void ExecuteDrawLabel(Rect position)
		{
			if (!this.IsDrawLabelOverriden())
			{
				foreach (var drawer in _fusumityDrawers)
				{
					if (drawer.IsDrawLabelOverriden())
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
			if (!this.IsDrawSubBodyOverriden())
			{
				foreach (var drawer in _fusumityDrawers)
				{
					if (drawer.IsDrawSubBodyOverriden())
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
			if (!this.IsDrawBodyOverriden())
			{
				foreach (var drawer in _fusumityDrawers)
				{
					if (drawer.IsDrawBodyOverriden())
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
			foreach (var drawer in _fusumityDrawers)
			{
				drawer.DrawAfterExtension(ref position);
			}
		}

		private void ExecuteOnPropertyChanged()
		{
			OnPropertyChanged();

			foreach (var drawer in _fusumityDrawers)
			{
				drawer.OnPropertyChanged();
			}
		}

		#endregion

		#region Custom

		public virtual void ModifyPropertyData() {}

		public virtual void ValidateBeforeDrawing() {}

		public virtual void DrawBeforeExtension(ref Rect position) {}

		public virtual void DrawLabelPrefix(Rect position)
		{
			EditorGUI.LabelField(position, currentPropertyData.labelPrefix, currentPropertyData.labelStyle);
		}

		public virtual void DrawLabel(Rect position)
		{
			EditorGUI.LabelField(position, currentPropertyData.label, currentPropertyData.labelStyle);
		}

		public virtual void DrawSubBody(Rect position)
		{
			currentPropertyData.property.DrawBody(position);
		}

		public virtual void DrawBody(Rect position)
		{
			currentPropertyData.property.DrawBody(position);
		}

		public virtual void DrawAfterExtension(ref Rect position) {}

		public virtual void OnPropertyChanged() {}

		#endregion
	}

	public enum EnableState
	{
		Enable,
		Disable,
		ForceEnable,
		ForceDisable
	}

	public class PropertyData
	{
		public bool forceBreak;

		public SerializedProperty property;
		public GUIContent label;

		public string labelPrefix;
		public float labelPrefixWidth;

		public bool drawPropertyChanged;
		public bool drawProperty;
		public EnableState enableState;

		public bool hasBeforeExtension;
		public bool hasFoldout;
		public bool hasLabel;
		public bool hasSubBody;
		public bool hasBody;
		public bool hasAfterExtension;

		public bool isArrayElement;

		public bool drawSubBodyWhenRollUp;
		public bool labelIntersectSubBody;

		public float beforeExtensionHeight;
		public float labelHeight;
		public float bodyHeight;
		public float afterExtensionHeight;

		public float drawOffsetY;
		public float drawOffsetX;
		public int indent;
		public int foldoutIndent;

		public GUIStyle labelStyle;

		public Color backgroundColor;

		private Dictionary<(object, Type), object> _persistentData = new();

		public void ResetData(SerializedProperty property, GUIContent label)
		{
			forceBreak = false;

			// GetPropertyHeight will singleLineHeight if no expanded
			var isExpanded = property.isExpanded;
			property.isExpanded = true;

			this.property = property;
			this.label = new GUIContent(label);

			labelPrefix = string.Empty;
			labelPrefixWidth = 0;

			drawPropertyChanged = false;
			drawProperty = true;
			enableState = EnableState.Enable;

			beforeExtensionHeight = 0f;
			labelHeight = EditorGUIUtility.singleLineHeight;
			bodyHeight = property.GetPropertyHeight(true);
			afterExtensionHeight = 0f;

			drawOffsetY = 0f;
			drawOffsetX = 0f;
			indent = 0;
			foldoutIndent = 0;

			isArrayElement = property.IsArrayElement();

			var hasChildren = bodyHeight > labelHeight && property.HasChildren();

			hasBeforeExtension = false;
			hasFoldout = hasChildren;
			hasLabel = true;
			hasSubBody = !hasChildren;
			hasBody = hasChildren;
			hasAfterExtension = false;

			drawSubBodyWhenRollUp = true;
			labelIntersectSubBody = true;
			if (hasChildren)
				bodyHeight -= labelHeight;

			labelStyle = EditorStyles.label;

			backgroundColor = GUI.backgroundColor;

			property.isExpanded = isExpanded;
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
			if (hasLabel || ShouldDrawSubBody())
			{
				height += labelHeight;
			}
			if (ShouldDrawBody())
			{
				height += bodyHeight;
			}
			if (hasAfterExtension)
			{
				height += afterExtensionHeight;
			}

			if (hasFoldout && height == 0f)
				height = EditorGUIUtility.singleLineHeight;

			height += drawOffsetY;
			return height;
		}

		public bool ShouldDrawSubBody()
		{
			return hasSubBody && !forceBreak && (property.isExpanded || !hasFoldout || drawSubBodyWhenRollUp);
		}

		public bool ShouldDrawBody()
		{
			return hasBody && !forceBreak && (property.isExpanded || !hasFoldout);
		}

		public bool ShouldDrawLabelPrefix()
		{
			return labelPrefix != null && labelPrefixWidth > 0;
		}

		public T GetPersistentData<T>(FusumityPropertyDrawer key)
		{
			if (_persistentData.TryGetValue((key, typeof(T)), out var result))
				return (T)result;
			return default;
		}

		public void SetPersistentData<T>(FusumityPropertyDrawer key, T value)
		{
			_persistentData[(key, typeof(T))] = value;
		}
	}
}
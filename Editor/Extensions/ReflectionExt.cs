using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Fusumity.Editor.Extensions
{
	public static class ReflectionExt
	{
		public const BindingFlags FIELD_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField;
		public const BindingFlags INTERNAL_FIELD_BINDING_FLAGS = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField;

		public const BindingFlags METHOD_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
		public const BindingFlags OVERRIDEN_METHOD_BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
		public const BindingFlags PRIVATE_METHOD_BINDING_FLAGS = BindingFlags.NonPublic | BindingFlags.Instance;

		public const char PATH_PARENT_CHAR = '/';
		public const char PATH_SPLIT_CHAR = '.';
		public const char ARRAY_INDEX_BEGINNER = '[';
		public const char ARRAY_DATA_TERMINATOR = ']';
		public const string ARRAY_DATA_BEGINNER = "data[";

		private static readonly Dictionary<Type, Type[]> ASSIGNABLE_FROM = new ();
		private static readonly Dictionary<Type, Type[]> TYPES_WITH_NULL = new ();
		private static readonly Dictionary<Type, Type[]> TYPES_WITHOUT_NULL = new ();

		private static readonly Dictionary<(object source, string methodPath), Action> COMPILED_ACTIONS = new ();

		public static bool IsList(this Type type)
		{
			if (!type.IsGenericType)
				return false;

			var genericTypeDefinition = type.GetGenericTypeDefinition();
			return genericTypeDefinition == typeof(List<>);
		}

		public static bool HasAttribute<T>(this MemberInfo memberInfo) where T: Attribute
		{
			return memberInfo.GetCustomAttribute<T>() != null;
		}

		public static List<FieldInfo> GetInstanceFields(this Type type, Type privateRestriction = null)
		{
			var fields = type.GetFields(FIELD_BINDING_FLAGS);
			var result = new List<FieldInfo>(fields);

			if (type != privateRestriction)
			{
				while (true)
				{
					type = type.BaseType;
					if (type == null || type == privateRestriction)
						break;
					fields = type.GetFields(INTERNAL_FIELD_BINDING_FLAGS);
					result.AddRange(fields);
				}
			}

			return result;
		}

		public static Type[] GetInheritorTypes(this Type baseType, bool insertNull = false, bool useGeneric = false)
		{
			Type[] inheritorTypes;
			if (insertNull)
			{
				if (TYPES_WITH_NULL.TryGetValue(baseType, out inheritorTypes))
					return inheritorTypes;
			}
			else if (TYPES_WITHOUT_NULL.TryGetValue(baseType, out inheritorTypes))
				return inheritorTypes;

			if (!ASSIGNABLE_FROM.TryGetValue(baseType, out var typeArray))
			{
				var assemblies = AppDomain.CurrentDomain.GetAssemblies();
				var typeList = new List<Type>();
				for (int a = 0; a < assemblies.Length; a++)
				{
					var types = assemblies[a].GetTypes();
					for (int t = 0; t < types.Length; t++)
					{
						if (baseType.IsAssignableFrom(types[t]) && !types[t].IsInterface && !types[t].IsAbstract && (useGeneric || !types[t].IsGenericType))
						{
							typeList.Add(types[t]);
						}
					}
				}

				typeArray = typeList.ToArray();
				ASSIGNABLE_FROM.Add(baseType, typeArray);
			}

			if (insertNull)
			{
				inheritorTypes = new Type[typeArray.Length + 1];
				Array.ConstrainedCopy(typeArray, 0, inheritorTypes, 1, typeArray.Length);
			}
			else
			{
				inheritorTypes = typeArray;
			}

			(insertNull ? TYPES_WITH_NULL : TYPES_WITHOUT_NULL).Add(baseType, inheritorTypes);

			return inheritorTypes;
		}

		public static void SetObjectByLocalPath(object source, string objectPath, object value)
		{
			var target = source;
			if (string.IsNullOrEmpty(objectPath))
				return;

			var pathComponents = objectPath.Split(PATH_SPLIT_CHAR);

			for (var p = 0; p < pathComponents.Length; p++)
			{
				var pathComponent = pathComponents[p];
				if (target is IList list)
				{
					if (p < pathComponents.Length - 2 && pathComponents[p + 1].StartsWith(ARRAY_DATA_BEGINNER))
					{
						var index = int.Parse(pathComponents[++p].Replace(ARRAY_DATA_BEGINNER, "").Replace($"{ARRAY_DATA_TERMINATOR}", ""));
						if (list.Count <= index)
							return;

						if (p + 1 == pathComponents.Length)
						{
							list[index] = value;
							return;
						}
						target = list[index];
					}
				}
				else
				{
					var field = GetAnyField(target.GetType(), pathComponent);

					if (p + 1 == pathComponents.Length)
					{
						field.SetValue(target, value);
						return;
					}
					target = field.GetValue(target);
				}
			}
		}

		public static object GetObjectByLocalPath(object source, string objectPath)
		{
			var target = source;
			if (string.IsNullOrEmpty(objectPath))
				return target;

			var pathComponents = objectPath.Split(PATH_SPLIT_CHAR);

			for (var p = 0; p < pathComponents.Length; p++)
			{
				var pathComponent = pathComponents[p];
				if (target is IList list)
				{
					if (p < pathComponents.Length - 1 && pathComponents[p + 1].StartsWith(ARRAY_DATA_BEGINNER))
					{
						var index = int.Parse(pathComponents[++p].Replace(ARRAY_DATA_BEGINNER, "").Replace($"{ARRAY_DATA_TERMINATOR}", ""));
						if (list.Count <= index)
							return null;
						target = list[index];
					}
				}
				else
				{
					var field = GetAnyField(target?.GetType(), pathComponent);
					if (field == null)
						return null;
					target = field.GetValue(target);
				}
			}

			return target;
		}

		public static Type GetTypeByLocalPath(object source, string propertyPath)
		{
			return GetObjectByLocalPath(source, propertyPath).GetType();
		}

		public static string GetParentPath(string propertyPath, bool includeArray = false)
		{
			return GetParentPath(propertyPath, out _, includeArray);
		}

		public static string GetParentPath(string propertyPath, out string localPath, bool includeArray = false)
		{
			var removeIndex = propertyPath.LastIndexOf(PATH_SPLIT_CHAR);
			if (removeIndex >= 0)
			{
				localPath = propertyPath.Remove(0, removeIndex + 1);
				propertyPath = propertyPath.Remove(removeIndex, propertyPath.Length - removeIndex);

				if (localPath[^1] != ARRAY_DATA_TERMINATOR)
					return propertyPath;

				// Remove "{field name}.Array"
				removeIndex = propertyPath.LastIndexOf(PATH_SPLIT_CHAR);
				localPath = propertyPath.Remove(0, removeIndex + 1) + PATH_SPLIT_CHAR + localPath;
				propertyPath = propertyPath.Remove(removeIndex, propertyPath.Length - removeIndex);

				if (includeArray)
					return propertyPath;

				removeIndex = propertyPath.LastIndexOf(PATH_SPLIT_CHAR);
				if (removeIndex < 0)
					return "";

				localPath = propertyPath.Remove(0, removeIndex + 1) + localPath;
				propertyPath = propertyPath.Remove(removeIndex, propertyPath.Length - removeIndex);

				return propertyPath;
			}
			else
			{
				localPath = propertyPath;
				return "";
			}
		}

		public static FieldInfo GetAnyField(this Type type, string fieldName)
		{
			if (type == null)
				return null;

			var field = type.GetField(fieldName, FIELD_BINDING_FLAGS);
			while (field == null)
			{
				type = type.BaseType;
				if (type == null)
					return null;
				field = type.GetField(fieldName, INTERNAL_FIELD_BINDING_FLAGS);
			}

			return field;
		}

		public static MethodInfo GetAnyMethod_WithoutArguments(this Type type, string methodName)
		{
			var methodInfo = type.GetMethod(methodName, METHOD_BINDING_FLAGS, null, new Type[]{}, null);
			while (methodInfo == null)
			{
				type = type.BaseType;
				if (type == null)
					return null;
				methodInfo = type.GetMethod(methodName, PRIVATE_METHOD_BINDING_FLAGS, null, new Type[]{}, null);
			}

			return methodInfo;
		}

		public static PropertyInfo GetAnyProperty(this Type type, string propertyName)
		{
			var propertyInfo = type.GetProperty(propertyName, METHOD_BINDING_FLAGS);
			while (propertyInfo == null)
			{
				type = type.BaseType;
				if (type == null)
					return null;
				propertyInfo = type.GetProperty(propertyName, METHOD_BINDING_FLAGS);
			}

			return propertyInfo;
		}

		public static object InvokeFuncByLocalPath(object source, string methodPath)
		{
			var targetPath = "";
			var methodName = methodPath;

			var removeIndex = methodPath.LastIndexOf(PATH_SPLIT_CHAR);
			if (removeIndex >= 0)
			{
				targetPath = methodPath.Remove(removeIndex, methodPath.Length - removeIndex);
				methodName = methodPath.Remove(0, removeIndex + 1);
			}

			var target = GetObjectByLocalPath(source, targetPath);
			var methodInfo = target?.GetType().GetAnyMethod_WithoutArguments(methodName);

			return methodInfo?.Invoke(target, null);
		}

		public static object InvokePropertyByLocalPath(object source, string propertyPath)
		{
			var targetPath = "";
			var propertyName = propertyPath;

			var removeIndex = propertyPath.LastIndexOf(PATH_SPLIT_CHAR);
			if (removeIndex >= 0)
			{
				targetPath = propertyPath.Remove(removeIndex, propertyPath.Length - removeIndex);
				propertyName = propertyPath.Remove(0, removeIndex + 1);
			}

			var target = GetObjectByLocalPath(source, targetPath);
			var propertyInfo = target?.GetType().GetAnyProperty(propertyName);

			return propertyInfo?.GetValue(target, null);
		}

		public static void InvokeMethodByLocalPath(object source, string methodPath)
		{
			var targetPath = "";
			var methodName = methodPath;

			var removeIndex = methodPath.LastIndexOf(PATH_SPLIT_CHAR);
			if (removeIndex >= 0)
			{
				targetPath = methodPath.Remove(removeIndex, methodPath.Length - removeIndex);
				methodName = methodPath.Remove(0, removeIndex + 1);
			}

			var target = GetObjectByLocalPath(source, targetPath);
			var methodInfo = target?.GetType().GetAnyMethod_WithoutArguments(methodName);

			methodInfo?.Invoke(target, null);
		}

		public static string AppendPath(this string sourcePath, string additionalPath)
		{
			if (string.IsNullOrEmpty(sourcePath))
				return additionalPath;
			if (string.IsNullOrEmpty(additionalPath))
				return sourcePath;

			while (additionalPath[0] == PATH_PARENT_CHAR)
			{
				additionalPath = additionalPath.Remove(0, 1);
				sourcePath = GetParentPath(sourcePath);
			}

			return sourcePath + PATH_SPLIT_CHAR + additionalPath;
		}

		public static string DequeuePathElement(this string sourcePath)
		{
			return sourcePath.DequeuePathElement(out _);
		}

		public static string DequeuePathElement(this string sourcePath, out string removedPath)
		{
			if (string.IsNullOrEmpty(sourcePath))
			{
				removedPath = string.Empty;
				return sourcePath;
			}

			var path = sourcePath.Split(PATH_SPLIT_CHAR, 4);

			if (path.Length == 1)
			{
				removedPath = sourcePath;
				return string.Empty;
			}

			// Check array: "{field name}.Array.data["
			if (path.Length >= 3 && path[2].StartsWith(ARRAY_DATA_BEGINNER))
			{
				removedPath = $"{path[0]}{PATH_SPLIT_CHAR}{path[1]}{PATH_SPLIT_CHAR}{path[2]}";
				return path.Length == 4 ? path[3] : string.Empty;
			}

			removedPath = path[0];

			var result = path[1];
			for (var i = 2; i < path.Length; i++)
				result = $"{result}{PATH_SPLIT_CHAR}{path[i]}";
			return result;
		}

		public static int GetArrayIndex(this string sourcePath)
		{
			if (string.IsNullOrEmpty(sourcePath) || sourcePath[^1] != ARRAY_DATA_TERMINATOR)
				return -1;

			var index = sourcePath.LastIndexOf(ARRAY_INDEX_BEGINNER);
			if (index < 0)
				return -1;
			index++;
			var value = sourcePath.Substring(index, sourcePath.Length - 1 - index);

			return int.TryParse(value, out var result) ? result : -1;
		}

		public static string SetArrayIndex(this string sourcePath, int newIndex)
		{
			if (string.IsNullOrEmpty(sourcePath) || sourcePath[^1] != ARRAY_DATA_TERMINATOR)
				return sourcePath;

			var index = sourcePath.LastIndexOf(ARRAY_INDEX_BEGINNER);
			if (index < 0)
				return sourcePath;
			var start = sourcePath.Substring(0, index + 1);

			return $"{start}{newIndex}{ARRAY_DATA_TERMINATOR}";
		}
	}
}
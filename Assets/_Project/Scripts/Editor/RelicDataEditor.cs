using System;
using UnityEditor;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.UI; // RelicRarityPalette: keeps the inspector chrome in sync with in-game colours

namespace RhythmRogue.EditorTools
{
    /// <summary>
    /// Custom inspector for <see cref="RelicData"/>.
    ///
    /// Two jobs: make a relic readable at a glance (a rarity-coloured banner with the icon and
    /// name, sectioned fields, colour swatches), and make effects authorable without reading the
    /// code (an "Add Effect" menu lists every <see cref="RelicEffectDef"/> by name; each effect
    /// draws its own labeled fields plus a live plain-language description). The labels come from
    /// the effect classes themselves, so they can never drift from what the effect actually does.
    ///
    /// The banner / accent colours come from <see cref="RelicRarityPalette"/>, the same source the
    /// reward, shop, detail card and HUD use, so the inspector previews the real in-game look.
    /// </summary>
    [CustomEditor(typeof(RelicData))]
    public class RelicDataEditor : UnityEditor.Editor
    {
        private SerializedProperty _relicName, _description, _flavorText, _rarity, _icon, _effects;

        private GUIStyle _describe, _section, _bannerTitle, _bannerSub, _effectTitle, _emptyHint;
        private bool _stylesReady;

        private void OnEnable()
        {
            _relicName = serializedObject.FindProperty("relicName");
            _description = serializedObject.FindProperty("description");
            _flavorText = serializedObject.FindProperty("flavorText");
            _rarity = serializedObject.FindProperty("rarity");
            _icon = serializedObject.FindProperty("icon");
            _effects = serializedObject.FindProperty("Effects");
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _describe = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.66f, 0.66f, 0.62f) },
                padding = new RectOffset(4, 4, 2, 2)
            };
            _section = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            _bannerTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            _bannerSub = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft };
            _effectTitle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
            _emptyHint = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontStyle = FontStyle.Italic };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EnsureStyles();

            DrawBanner();

            EditorGUILayout.Space(6);
            DrawSection("Identity");
            EditorGUILayout.PropertyField(_relicName);
            EditorGUILayout.PropertyField(_description);
            EditorGUILayout.PropertyField(_flavorText);
            EditorGUILayout.PropertyField(_rarity);

            EditorGUILayout.Space(8);
            DrawSection("Display");
            EditorGUILayout.PropertyField(_icon);
            DrawColourPreview();

            EditorGUILayout.Space(8);
            DrawSection("Effects");
            DrawEffects();

            serializedObject.ApplyModifiedProperties();
        }

        // ----------------------------------------------------------------
        // Header banner
        // ----------------------------------------------------------------
        private void DrawBanner()
        {
            RelicRarity rarity = CurrentRarity();
            Color bg = RelicRarityPalette.Background(rarity);
            Color accent = RelicRarityPalette.Accent(rarity);

            Rect r = GUILayoutUtility.GetRect(0, 54, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, bg);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 5f, r.height), accent); // accent spine

            // Icon thumbnail (assigned art, else a faint placeholder block).
            float iconSize = r.height - 14f;
            var iconRect = new Rect(r.x + 14f, r.y + 7f, iconSize, iconSize);
            var sprite = _icon != null ? _icon.objectReferenceValue as Sprite : null;
            if (sprite != null) DrawSprite(iconRect, sprite);
            else EditorGUI.DrawRect(iconRect, new Color(accent.r, accent.g, accent.b, 0.25f));

            float textX = iconRect.xMax + 12f;
            string name = _relicName != null && !string.IsNullOrEmpty(_relicName.stringValue)
                ? _relicName.stringValue
                : "(unnamed relic)";
            GUI.Label(new Rect(textX, r.y + 8f, r.width - textX - 10f, 22f), name, _bannerTitle);

            _bannerSub.normal.textColor = accent;
            GUI.Label(new Rect(textX, r.y + 30f, r.width - textX - 10f, 16f),
                $"{rarity.ToString().ToUpper()} RELIC", _bannerSub);
        }

        // ----------------------------------------------------------------
        // Section header
        // ----------------------------------------------------------------
        private void DrawSection(string title)
        {
            Rect r = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, 0.05f));
            EditorGUI.DrawRect(new Rect(r.x, r.y, 3f, r.height), RelicRarityPalette.Accent(CurrentRarity()));
            GUI.Label(new Rect(r.x + 10f, r.y + 2f, r.width - 12f, r.height - 2f), title, _section);
        }

        // ----------------------------------------------------------------
        // Card colour preview (swatches for the rarity-driven colours)
        // ----------------------------------------------------------------
        private void DrawColourPreview()
        {
            RelicRarity rarity = CurrentRarity();
            Rect r = EditorGUILayout.GetControlRect(true, 20f);

            GUI.Label(new Rect(r.x, r.y, EditorGUIUtility.labelWidth, r.height), "Card Colour");

            float x = r.x + EditorGUIUtility.labelWidth;
            DrawSwatch(new Rect(x, r.y + 1f, 38f, r.height - 2f), RelicRarityPalette.Background(rarity));
            DrawSwatch(new Rect(x + 44f, r.y + 1f, 38f, r.height - 2f), RelicRarityPalette.Accent(rarity));
            GUI.Label(new Rect(x + 90f, r.y, r.width - (x + 90f - r.x), r.height),
                $"auto from rarity ({rarity})", EditorStyles.miniLabel);
        }

        private static void DrawSwatch(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), new Color(0, 0, 0, 0.5f));
            EditorGUI.DrawRect(rect, color);
        }

        // ----------------------------------------------------------------
        // Effects
        // ----------------------------------------------------------------
        private void DrawEffects()
        {
            if (_effects == null)
            {
                EditorGUILayout.HelpBox("No Effects property found on this asset.", MessageType.Error);
                return;
            }

            if (_effects.arraySize == 0)
            {
                var box = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(box, new Color(1f, 1f, 1f, 0.03f));
                GUI.Label(box, "No effects yet  -  click Add Effect below", _emptyHint);
            }

            Color accent = RelicRarityPalette.Accent(CurrentRarity());
            int removeIndex = -1;

            for (int i = 0; i < _effects.arraySize; i++)
            {
                SerializedProperty element = _effects.GetArrayElementAtIndex(i);
                var def = element.managedReferenceValue as RelicEffectDef;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Accent-tinted header bar: spine + title + Remove.
                Rect hb = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(hb, new Color(accent.r, accent.g, accent.b, 0.18f));
                EditorGUI.DrawRect(new Rect(hb.x, hb.y, 3f, hb.height), accent);
                GUI.Label(new Rect(hb.x + 8f, hb.y + 2f, hb.width - 78f, hb.height - 2f),
                    def?.DisplayName ?? "(empty)", _effectTitle);
                if (GUI.Button(new Rect(hb.xMax - 66f, hb.y + 1f, 62f, hb.height - 3f), "Remove", EditorStyles.miniButton))
                    removeIndex = i;

                EditorGUILayout.Space(3);
                DrawManagedChildren(element);

                if (def != null)
                {
                    string sentence = def.Describe();
                    if (!string.IsNullOrEmpty(sentence))
                        EditorGUILayout.LabelField(sentence, _describe);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
            }

            if (removeIndex >= 0)
            {
                // Null the managed reference first, then delete the slot, so no dangling
                // reference is left behind on the array.
                _effects.GetArrayElementAtIndex(removeIndex).managedReferenceValue = null;
                _effects.DeleteArrayElementAtIndex(removeIndex);
            }

            EditorGUILayout.Space(2);
            if (GUILayout.Button("+  Add Effect", GUILayout.Height(24)))
                ShowAddMenu();
        }

        /// <summary>
        /// Draw the visible child properties of a managed-reference element (the effect's own
        /// fields), without the extra type-name foldout PropertyField would add.
        /// </summary>
        private static void DrawManagedChildren(SerializedProperty element)
        {
            SerializedProperty end = element.GetEndProperty();
            SerializedProperty child = element.Copy();
            bool enterChildren = true;
            EditorGUI.indentLevel++;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                EditorGUILayout.PropertyField(child, true);
            }
            EditorGUI.indentLevel--;
        }

        private void ShowAddMenu()
        {
            var menu = new GenericMenu();
            foreach (Type t in TypeCache.GetTypesDerivedFrom<RelicEffectDef>())
            {
                if (t.IsAbstract) continue;
                Type captured = t;
                string label;
                try { label = (Activator.CreateInstance(captured) as RelicEffectDef)?.DisplayName ?? captured.Name; }
                catch { label = captured.Name; }
                menu.AddItem(new GUIContent(label), false, () => AddEffect(captured));
            }
            menu.ShowAsContext();
        }

        private void AddEffect(Type effectType)
        {
            serializedObject.Update();
            int index = _effects.arraySize;
            _effects.arraySize++;
            SerializedProperty element = _effects.GetArrayElementAtIndex(index);
            element.managedReferenceValue = Activator.CreateInstance(effectType);
            serializedObject.ApplyModifiedProperties();
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        /// <summary>Current rarity from the serialized enum (defaults to Common if mixed/invalid).</summary>
        private RelicRarity CurrentRarity()
        {
            int idx = _rarity != null ? _rarity.enumValueIndex : 0;
            if (idx < 0) idx = 0;
            return (RelicRarity)idx;
        }

        /// <summary>Draw a Sprite into a rect (handles the sprite's region within its texture).</summary>
        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;
            Texture2D tex = sprite.texture;
            Rect sr = sprite.rect;
            var coords = new Rect(sr.x / tex.width, sr.y / tex.height, sr.width / tex.width, sr.height / tex.height);
            GUI.DrawTextureWithTexCoords(rect, tex, coords);
        }
    }
}

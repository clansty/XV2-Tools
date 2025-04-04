﻿using LB_Common.Numbers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Xv2CoreLib.EMB_CLASS;
using Xv2CoreLib.EMM;
using Xv2CoreLib.HslColor;
using Xv2CoreLib.Resource;
using Xv2CoreLib.Resource.UndoRedo;
using YAXLib;

namespace Xv2CoreLib.EMP
{
    //This EMP parser is obsolete and only kept here for XML serialization since the newer one is incapable of that (intentionally).

    public enum ParserMode
    {
        [Description("The resulting EMP_File object will be optimzied for XML output.")]
        Xml,
        [Description("The resulting EMP_File object will be ideal for tool manipulation. Do not serialize this to XML.\n\nChanges:\nTextures will be linked via object ref, not ID.")]
        Tool
    }

    public enum VersionEnum : ushort
    {
        DBXV2 = 37568,
        SDBH = 37632
    }

    [Serializable]
    [YAXSerializeAs("EMP")]
    [Obsolete("This EMP parser is obsolete and only used for XML now. Use Xv2CoreLib.EMP instead!")]
    public class EMP_File
    {
        public const int EMP_SIGNATURE = 1347241251;

        [YAXAttributeForClass]
        public VersionEnum Version { get; set; } = VersionEnum.DBXV2;

        [YAXDontSerializeIfNull]
        public AsyncObservableCollection<ParticleEffect> ParticleEffects { get; set; } = new AsyncObservableCollection<ParticleEffect>();
        [YAXDontSerializeIfNull]
        public AsyncObservableCollection<EMP_TextureDefinition> Textures { get; set; } = new AsyncObservableCollection<EMP_TextureDefinition>();

        #region LoadSave
        public byte[] SaveToBytes(ParserMode _parserMode)
        {
            return new Deserializer(this, _parserMode).bytes.ToArray();
        }

        public static EMP_File Load(string path)
        {
            return new Parser(path, false).GetEmpFile();
        }

        public static EMP_File Load(byte[] bytes, ParserMode parserMode)
        {
            return new Parser(bytes, parserMode).GetEmpFile();
        }

        #endregion

        #region References
        /// <summary>
        /// Parses all ParticleEffects and removes the specified Texture ID, if found.
        /// </summary>
        /// <param name="ID"></param>
        public void RemoveTextureReferences(int ID)
        {
            if (ParticleEffects != null)
            {
                RemoveTextureReferences_Recursive(ParticleEffects, ID);
            }
        }

        private void RemoveTextureReferences_Recursive(AsyncObservableCollection<ParticleEffect> children, int ID)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].ChildParticleEffects != null)
                {
                    RemoveTextureReferences_Recursive(children[i].ChildParticleEffects, ID);
                }

                if (children[i].Type_Texture != null)
                {
                    foreach (var e in children[i].Type_Texture.TextureIndex)
                    {
                        if (e == ID)
                        {
                            children[i].Type_Texture.TextureIndex.Remove(e);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Parses all ParticleEffects and removes the specified Texture ref, if found.
        /// </summary>
        public void RemoveTextureReferences(EMP_TextureDefinition textureRef, List<IUndoRedo> undos = null)
        {
            if (ParticleEffects != null)
            {
                RemoveTextureReferences_Recursive(ParticleEffects, textureRef, undos);
            }
        }

        private void RemoveTextureReferences_Recursive(AsyncObservableCollection<ParticleEffect> children, EMP_TextureDefinition textureRef, List<IUndoRedo> undos = null)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].ChildParticleEffects != null)
                {
                    RemoveTextureReferences_Recursive(children[i].ChildParticleEffects, textureRef, undos);
                }

                if (children[i].Type_Texture != null)
                {
                startPoint:
                    foreach (var e in children[i].Type_Texture.TextureEntryRef)
                    {
                        if (e.TextureRef == textureRef)
                        {
                            if (undos != null)
                                undos.Add(new UndoableListRemove<TextureEntry_Ref>(children[i].Type_Texture.TextureEntryRef, e));

                            children[i].Type_Texture.TextureEntryRef.Remove(e);
                            goto startPoint;
                        }
                    }
                }
            }
        }

        public void RefactorTextureRef(EMP_TextureDefinition oldTextureRef, EMP_TextureDefinition newTextureRef, List<IUndoRedo> undos)
        {
            if (ParticleEffects != null)
            {
                RemoveTextureReferences_Recursive(ParticleEffects, oldTextureRef, newTextureRef, undos);
            }
        }

        private void RemoveTextureReferences_Recursive(AsyncObservableCollection<ParticleEffect> children, EMP_TextureDefinition oldTextureRef, EMP_TextureDefinition newTextureRef, List<IUndoRedo> undos)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].ChildParticleEffects != null)
                {
                    RemoveTextureReferences_Recursive(children[i].ChildParticleEffects, oldTextureRef, newTextureRef, undos);
                }

                if (children[i].Type_Texture != null)
                {
                    foreach (var e in children[i].Type_Texture.TextureEntryRef)
                    {
                        if (e.TextureRef == oldTextureRef)
                        {
                            undos.Add(new UndoableProperty<TextureEntry_Ref>(nameof(e.TextureRef), e, e.TextureRef, newTextureRef));
                            e.TextureRef = newTextureRef;
                        }
                    }
                }
            }
        }

        #endregion

        #region AddRemove
        /// <summary>
        /// Add a new ParticleEffect entry.
        /// </summary>
        /// <param name="index">Where in the collection to insert the new entry. The default value of -1 will result in it being added to the end, as will out of range values.</param>
        public void AddNew(int index = -1, List<IUndoRedo> undos = null)
        {
            if (index < -1 || index > ParticleEffects.Count() - 1) index = -1;

            var newEffect = ParticleEffect.GetNew();

            if (index == -1)
            {
                ParticleEffects.Add(newEffect);

                if (undos != null)
                    undos.Add(new UndoableListAdd<ParticleEffect>(ParticleEffects, newEffect));
            }
            else
            {
                ParticleEffects.Insert(index, newEffect);

                if (undos != null)
                    undos.Add(new UndoableStateChange<ParticleEffect>(ParticleEffects, index, ParticleEffects[index], newEffect));
            }
        }

        public bool RemoveParticleEffect(ParticleEffect effectToRemove, List<IUndoRedo> undos = null)
        {
            bool result = false;
            for (int i = 0; i < ParticleEffects.Count; i++)
            {
                result = RemoveInChildren(ParticleEffects[i].ChildParticleEffects, effectToRemove, undos);

                if (ParticleEffects[i] == effectToRemove)
                {
                    if (undos != null)
                        undos.Add(new UndoableListRemove<ParticleEffect>(ParticleEffects, effectToRemove));

                    ParticleEffects.Remove(effectToRemove);
                    return true;
                }

                if (result == true)
                {
                    break;
                }
            }

            return result;
        }

        private bool RemoveInChildren(AsyncObservableCollection<ParticleEffect> children, ParticleEffect effectToRemove, List<IUndoRedo> undos = null)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].ChildParticleEffects.Count > 0)
                {
                    RemoveInChildren(children[i].ChildParticleEffects, effectToRemove, undos);
                }

                if (children[i] == effectToRemove)
                {
                    if (undos != null)
                        undos.Add(new UndoableListRemove<ParticleEffect>(children, effectToRemove));

                    children.Remove(effectToRemove);
                    return true;
                }

            }

            return false;
        }

        #endregion

        #region Get
        public AsyncObservableCollection<ParticleEffect> GetParentList(ParticleEffect particleEffect)
        {
            foreach (var e in ParticleEffects)
            {
                AsyncObservableCollection<ParticleEffect> result = null;

                if (e.ChildParticleEffects.Count > 0)
                {
                    result = GetParentList_Recursive(e.ChildParticleEffects, particleEffect);
                }
                if (result != null)
                {
                    return result;
                }

                if (e == particleEffect)
                {
                    return ParticleEffects;
                }
            }

            return null;
        }

        private AsyncObservableCollection<ParticleEffect> GetParentList_Recursive(AsyncObservableCollection<ParticleEffect> children, ParticleEffect particleEffect)
        {
            AsyncObservableCollection<ParticleEffect> result = null;

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].ChildParticleEffects.Count > 0)
                {
                    result = GetParentList_Recursive(children[i].ChildParticleEffects, particleEffect);
                }

                if (children[i] == particleEffect)
                {
                    return children;
                }
            }

            if (result != null)
            {
                return result;
            }
            else
            {
                return null;
            }
        }

        public List<EMP_TextureDefinition> GetTextureEntriesThatUseRef(EMB_CLASS.EmbEntry textureRef)
        {
            List<EMP_TextureDefinition> textures = new List<EMP_TextureDefinition>();

            foreach (var texture in Textures)
            {
                if (texture.TextureRef == textureRef)
                {
                    textures.Add(texture);
                }
            }

            return textures;
        }

        public List<TexturePart> GetTexturePartsThatUseMaterialRef(EmmMaterial materialRef)
        {
            List<TexturePart> textureParts = new List<TexturePart>();

            foreach (var particleEffect in ParticleEffects)
            {
                if (particleEffect.Type_Texture.MaterialRef == materialRef)
                {
                    textureParts.Add(particleEffect.Type_Texture);
                }

                if (particleEffect.ChildParticleEffects != null)
                {
                    textureParts = GetTexturePartsThatUseMaterialRef_Recursive(materialRef, textureParts, particleEffect.ChildParticleEffects);
                }

            }

            return textureParts;
        }

        private List<TexturePart> GetTexturePartsThatUseMaterialRef_Recursive(EmmMaterial materialRef, List<TexturePart> textureParts, AsyncObservableCollection<ParticleEffect> particleEffects)
        {
            foreach (var particleEffect in particleEffects)
            {
                if (particleEffect.Type_Texture.MaterialRef == materialRef)
                {
                    textureParts.Add(particleEffect.Type_Texture);
                }

                if (particleEffect.ChildParticleEffects != null)
                {
                    textureParts = GetTexturePartsThatUseMaterialRef_Recursive(materialRef, textureParts, particleEffect.ChildParticleEffects);
                }

            }

            return textureParts;
        }

        public List<TexturePart> GetTexturePartsThatUseEmbEntryRef(EMP_TextureDefinition embEntryRef)
        {
            List<TexturePart> textureParts = new List<TexturePart>();

            foreach (var particleEffect in ParticleEffects)
            {
                foreach (var textureEntry in particleEffect.Type_Texture.TextureEntryRef)
                {
                    if (textureEntry.TextureRef == embEntryRef)
                    {
                        textureParts.Add(particleEffect.Type_Texture);
                        break;
                    }
                }

                if (particleEffect.ChildParticleEffects != null)
                {
                    textureParts = GetTexturePartsThatUseEmbEntryRef_Recursive(embEntryRef, textureParts, particleEffect.ChildParticleEffects);
                }

            }

            return textureParts;
        }

        private List<TexturePart> GetTexturePartsThatUseEmbEntryRef_Recursive(EMP_TextureDefinition embEntryRef, List<TexturePart> textureParts, AsyncObservableCollection<ParticleEffect> particleEffects)
        {
            foreach (var particleEffect in particleEffects)
            {
                foreach (var textureEntry in particleEffect.Type_Texture.TextureEntryRef)
                {
                    if (textureEntry.TextureRef == embEntryRef)
                    {
                        textureParts.Add(particleEffect.Type_Texture);
                        break;
                    }
                }

                if (particleEffect.ChildParticleEffects != null)
                {
                    textureParts = GetTexturePartsThatUseEmbEntryRef_Recursive(embEntryRef, textureParts, particleEffect.ChildParticleEffects);
                }

            }

            return textureParts;
        }

        /// <summary>
        /// Finds an identical texture. Returns null if none exists.
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        public EMP_TextureDefinition GetTexture(EMP_TextureDefinition texture)
        {
            if (texture == null) return null;

            foreach (var tex in Textures)
            {
                if (tex.Compare(texture)) return tex;
            }

            return null;
        }

        #endregion

        #region Color

        public List<RgbColor> GetUsedColors()
        {
            List<RgbColor> colors = new List<RgbColor>();
            if (ParticleEffects == null) return colors;

            foreach (var particleEffect in ParticleEffects)
            {
                colors.AddRange(particleEffect.GetUsedColors());
            }

            return colors;
        }

        public void ChangeHue(double hue, double saturation, double lightness, List<IUndoRedo> undos = null, bool hueSet = false, int variance = 0)
        {
            if (ParticleEffects == null) return;
            if (undos == null) undos = new List<IUndoRedo>();

            foreach (var particleEffects in ParticleEffects)
            {
                particleEffects.ChangeHue(hue, saturation, lightness, undos, hueSet, variance);
            }
        }

        public List<IUndoRedo> RemoveColorAnimations(AsyncObservableCollection<ParticleEffect> particleEffects = null, bool root = true)
        {
            if (particleEffects == null && root) particleEffects = ParticleEffects;
            if (particleEffects == null && !root) return new List<IUndoRedo>();

            List<IUndoRedo> undos = new List<IUndoRedo>();

            foreach (var particleEffect in particleEffects)
            {
                particleEffect.RemoveColorType0Animations(undos);
                particleEffect.RemoveColorType1Animations(undos);

                if (particleEffect.ChildParticleEffects != null)
                    undos.AddRange(RemoveColorAnimations(particleEffect.ChildParticleEffects, false));
            }

            return undos;
        }

        public List<IUndoRedo> RemoveRandomColorRange(AsyncObservableCollection<ParticleEffect> particleEffects = null, bool root = true)
        {
            if (particleEffects == null && root) particleEffects = ParticleEffects;
            if (particleEffects == null && !root) return new List<IUndoRedo>();

            List<IUndoRedo> undos = new List<IUndoRedo>();

            foreach (var particleEffect in particleEffects)
            {
                particleEffect.RemoveColorRandomRange(undos);

                if (particleEffect.ChildParticleEffects != null)
                    undos.AddRange(RemoveRandomColorRange(particleEffect.ChildParticleEffects, false));
            }

            return undos;
        }

        #endregion

        public EMP_File Clone()
        {
            AsyncObservableCollection<ParticleEffect> _ParticleEffects = new AsyncObservableCollection<ParticleEffect>();
            AsyncObservableCollection<EMP_TextureDefinition> _Textures = new AsyncObservableCollection<EMP_TextureDefinition>();
            foreach (var e in ParticleEffects)
            {
                _ParticleEffects.Add(e.Clone());
            }
            foreach (var e in Textures)
            {
                _Textures.Add(e.Clone());
            }

            return new EMP_File()
            {
                ParticleEffects = _ParticleEffects,
                Textures = _Textures
            };
        }

        public int NextTextureId()
        {
            int id = 0;

            while (Textures.Any(x => x.EntryIndex == id))
                id++;

            return id;
        }

        //DEBUG:
        public List<ParticleEffect> GetAllParticleEffects_DEBUG()
        {
            List<ParticleEffect> GetAllParticleEffectsRecursive_DEBUG(IList<ParticleEffect> particleEffects)
            {
                List<ParticleEffect> total = new List<ParticleEffect>();

                foreach (var particle in particleEffects)
                {
                    total.Add(particle);

                    if (particle.ChildParticleEffects != null)
                    {
                        total.AddRange(GetAllParticleEffectsRecursive_DEBUG(particle.ChildParticleEffects));
                    }
                }

                return total;
            }

            return GetAllParticleEffectsRecursive_DEBUG(ParticleEffects);
        }
    }

    //Section1/MainEntry

    [Serializable]
    public class ParticleEffect : INotifyPropertyChanged
    {
        #region UI
        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
        
        public void NotifyPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private bool isExpanded = false;
        [YAXDontSerialize]
        public bool IsExpanded
        {
            get
            {
                return isExpanded;
            }

            set
            {
                if (value != isExpanded)
                {
                    isExpanded = value;
                    NotifyPropertyChanged(nameof(IsExpanded));
                }
            }
        }
        private bool isSelected = false;
        [YAXDontSerialize]
        public bool IsSelected
        {
            get
            {
                return isSelected;
            }

            set
            {
                if (value != isSelected)
                {
                    isSelected = value;
                    NotifyPropertyChanged(nameof(IsSelected));
                }
            }
        }

        #endregion

        public enum ComponentType
        {
            None, //"null"
            VerticalDistribution,
            SphericalDistribution,
            ShapePerimeterDistribution,
            ShapeAreaDistribution,
            AutoOriented,
            AutoOriented_VisibleOnSpeed,
            Default,
            ConeExtrude,
            Mesh,
            ShapeDraw
        }

        [YAXDontSerialize]
        public IEnumerable<ComponentType> ComponentTypeValues
        {
            get
            {
                return Enum.GetValues(typeof(ComponentType))
                    .Cast<ComponentType>();
            }
        }


        private string _name = null;
        [YAXAttributeForClass]
        [YAXSerializeAs("Name")]
        public string Name
        {
            get
            {
                return this._name;
            }

            set
            {
                if (value != this._name)
                {
                    this._name = value;
                    NotifyPropertyChanged("Name");
                }
            }
        }

        [YAXAttributeForClass]
        [YAXSerializeAs("Type")]
        public ComponentType Component_Type { get; set; }

        //Start
        [YAXAttributeFor("StartTime")]
        [YAXSerializeAs("Base")]
        public byte I_44 { get; set; }
        [YAXAttributeFor("StartTime")]
        [YAXSerializeAs("AddedRandom")]
        public byte I_45 { get; set; }
        [YAXAttributeFor("Loop")]
        [YAXSerializeAs("value")]
        public bool I_32_1 { get; set; }
        [YAXAttributeFor("FlashOnGeneration")]
        [YAXSerializeAs("value")]
        public bool I_32_3 { get; set; }
        [YAXAttributeFor("NumberOfParticles")]
        [YAXSerializeAs("value")]
        public short I_38 { get; set; }
        [YAXAttributeFor("Particle_Lifetime")]
        [YAXSerializeAs("Base")]
        public short I_40 { get; set; }
        [YAXAttributeFor("Particle_Lifetime")]
        [YAXSerializeAs("AddedRandom")]
        public ushort I_42 { get; set; }
        [YAXAttributeFor("Hide")]
        [YAXSerializeAs("value")]
        public bool I_33_3 { get; set; }
        [YAXAttributeFor("UseScale2")]
        [YAXSerializeAs("value")]
        public bool I_33_4 { get; set; }
        [YAXAttributeFor("UseColor2")]
        [YAXSerializeAs("value")]
        public bool I_33_6 { get; set; }
        [YAXAttributeFor("AutoOrientationType")]
        [YAXSerializeAs("value")]
        public AutoOrientationType I_35 { get; set; } //int8
        [YAXAttributeFor("NumberOfFramesBetweenNewParticles")]
        [YAXSerializeAs("Base")]
        public byte I_46 { get; set; }
        [YAXAttributeFor("NumberOfFramesBetweenNewParticles")]
        [YAXSerializeAs("AddedRandom")]
        public byte I_47 { get; set; }
        [YAXAttributeFor("MaxParticlesPerFrame")]
        [YAXSerializeAs("Base")]
        public ushort I_52 { get; set; }
        [YAXAttributeFor("MaxParticlesPerFrame")]
        [YAXSerializeAs("AddedRandom")]
        public ushort I_54 { get; set; }

        [YAXAttributeFor("PositionOffset")]
        [YAXSerializeAs("X")]
        [YAXFormat("0.0###########")]
        public float F_64 { get; set; }
        [YAXAttributeFor("PositionOffset")]
        [YAXSerializeAs("Y")]
        [YAXFormat("0.0###########")]
        public float F_68 { get; set; }
        [YAXAttributeFor("PositionOffset")]
        [YAXSerializeAs("Z")]
        [YAXFormat("0.0###########")]
        public float F_72 { get; set; }
        [YAXAttributeFor("PositionOffset")]
        [YAXSerializeAs("W")]
        [YAXFormat("0.0###########")]
        public float F_76 { get; set; }
        [YAXAttributeFor("PositionOffset_AddedRandom")]
        [YAXSerializeAs("X")]
        [YAXFormat("0.0###########")]
        public float F_80 { get; set; }
        [YAXAttributeFor("PositionOffset_AddedRandom")]
        [YAXSerializeAs("Y")]
        [YAXFormat("0.0###########")]
        public float F_84 { get; set; }
        [YAXAttributeFor("PositionOffset_AddedRandom")]
        [YAXSerializeAs("Z")]
        [YAXFormat("0.0###########")]
        public float F_88 { get; set; }
        [YAXAttributeFor("PositionOffset_AddedRandom")]
        [YAXSerializeAs("W")]
        [YAXFormat("0.0###########")]
        public float F_92 { get; set; }
        [YAXAttributeFor("Rotation")]
        [YAXSerializeAs("X")]
        [YAXFormat("0.0###########")]
        public float F_96 { get; set; }
        [YAXAttributeFor("Rotation")]
        [YAXSerializeAs("Y")]
        [YAXFormat("0.0###########")]
        public float F_100 { get; set; }
        [YAXAttributeFor("Rotation")]
        [YAXSerializeAs("Z")]
        [YAXFormat("0.0###########")]
        public float F_104 { get; set; }
        [YAXAttributeFor("Rotation")]
        [YAXSerializeAs("W")]
        [YAXFormat("0.0###########")]
        public float F_108 { get; set; }
        [YAXAttributeFor("Rotation_AddedRandom")]
        [YAXSerializeAs("X")]
        [YAXFormat("0.0###########")]
        public float F_112 { get; set; }
        [YAXAttributeFor("Rotation_AddedRandom")]
        [YAXSerializeAs("Y")]
        [YAXFormat("0.0###########")]
        public float F_116 { get; set; }
        [YAXAttributeFor("Rotation_AddedRandom")]
        [YAXSerializeAs("Z")]
        [YAXFormat("0.0###########")]
        public float F_120 { get; set; }
        [YAXAttributeFor("Rotation_AddedRandom")]
        [YAXSerializeAs("W")]
        [YAXFormat("0.0###########")]
        public float F_124 { get; set; }
        [YAXAttributeFor("EnableRandomRotationDirection")]
        [YAXSerializeAs("value")]
        public bool I_34_4 { get; set; }
        [YAXAttributeFor("EnableRandomUpVectorOnVirtualCone")]
        [YAXSerializeAs("value")]
        public bool I_34_6 { get; set; }
        [YAXAttributeFor("F_128")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_128 { get; set; }
        [YAXAttributeFor("F_132")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_132 { get; set; }
        [YAXAttributeFor("I_136")]
        [YAXSerializeAs("value")]
        public ushort I_136 { get; set; }

        //Unknown values
        [YAXAttributeFor("Flags_32")]
        [YAXSerializeAs("Unk0")]
        public bool I_32_0 { get; set; }
        [YAXAttributeFor("Flags_32")]
        [YAXSerializeAs("Unk2")]
        public bool I_32_2 { get; set; }
        [YAXAttributeFor("Flags_32")]
        [YAXSerializeAs("Unk4")]
        public bool I_32_4 { get; set; }
        [YAXAttributeFor("Flags_32")]
        [YAXSerializeAs("Unk5")]
        public bool I_32_5 { get; set; }
        [YAXAttributeFor("Flags_32")]
        [YAXSerializeAs("Unk6")]
        public bool I_32_6 { get; set; }
        [YAXAttributeFor("Flags_32")]
        [YAXSerializeAs("Unk7")]
        public bool I_32_7 { get; set; }


        //I_33 test
        [YAXAttributeFor("Flags_33")]
        [YAXSerializeAs("Unk0")]
        public bool I_33_0 { get; set; }
        [YAXAttributeFor("Flags_33")]
        [YAXSerializeAs("Unk1")]
        public bool I_33_1 { get; set; }
        [YAXAttributeFor("Flags_33")]
        [YAXSerializeAs("Unk2")]
        public bool I_33_2 { get; set; }
        [YAXAttributeFor("Flags_33")]
        [YAXSerializeAs("Unk5")]
        public bool I_33_5 { get; set; }
        [YAXAttributeFor("Flags_33")]
        [YAXSerializeAs("Unk7")]
        public bool I_33_7 { get; set; }
        //I_34 test
        [YAXAttributeFor("Flags_34")]
        [YAXSerializeAs("Unk0")]
        public bool I_34_0 { get; set; }
        [YAXAttributeFor("Flags_34")]
        [YAXSerializeAs("Unk1")]
        public bool I_34_1 { get; set; }
        [YAXAttributeFor("Flags_34")]
        [YAXSerializeAs("Unk2")]
        public bool I_34_2 { get; set; }
        [YAXAttributeFor("Flags_34")]
        [YAXSerializeAs("Unk3")]
        public bool I_34_3 { get; set; }
        [YAXAttributeFor("Flags_34")]
        [YAXSerializeAs("Unk5")]
        public bool I_34_5 { get; set; }
        [YAXAttributeFor("Flags_34")]
        [YAXSerializeAs("Unk7")]
        public bool I_34_7 { get; set; }
        [YAXAttributeFor("I_48")]
        [YAXSerializeAs("value")]
        public ushort I_48 { get; set; }
        [YAXAttributeFor("I_50")]
        [YAXSerializeAs("value")]
        public ushort I_50 { get; set; }
        [YAXAttributeFor("I_56")]
        [YAXSerializeAs("value")]
        public ushort I_56 { get; set; }
        [YAXAttributeFor("I_58")]
        [YAXSerializeAs("value")]
        public ushort I_58 { get; set; }
        [YAXAttributeFor("I_60")]
        [YAXSerializeAs("value")]
        public ushort I_60 { get; set; }
        [YAXAttributeFor("I_62")]
        [YAXSerializeAs("value")]
        public ushort I_62 { get; set; }

        //ExtraPart data

        [YAXDontSerializeIfNull]
        [YAXSerializeAs("TexturePart")]
        public TexturePart Type_Texture { get; set; }

        [YAXDontSerializeIfNull]
        [YAXSerializeAs("VerticalDistribution_Component")]
        public FloatPart_0_1 FloatPart_00_01 { get; set; }
        [YAXDontSerializeIfNull]
        [YAXSerializeAs("SphericalDistribution_Component")]
        public FloatPart_1_1 FloatPart_01_01 { get; set; }
        [YAXDontSerializeIfNull]
        [YAXSerializeAs("ShapePerimeterDistribution_Component")]
        public FloatPart_2_1 FloatPart_02_01 { get; set; }
        [YAXDontSerializeIfNull]
        [YAXSerializeAs("ShapeAreaDistribution_Component")]
        public FloatPart_3_1 FloatPart_03_01 { get; set; }
        [YAXDontSerializeIfNull]
        [YAXSerializeAs("AutoOriented_Component")]
        public FloatPart_0_2 FloatPart_00_02 { get; set; }
        [YAXDontSerializeIfNull]
        [YAXSerializeAs("Default_Component")]
        public FloatPart_2_2 FloatPart_02_02 { get; set; }
        [YAXDontSerializeIfNull]
        [YAXSerializeAs("ConeExtrude_Component")]
        public Struct3 Type_Struct3 { get; set; }
        [YAXDontSerializeIfNull]
        [YAXSerializeAs("ShapeDraw_Component")]
        public Struct5 Type_Struct5 { get; set; }
        [YAXDontSerializeIfNull]
        [YAXSerializeAs("Mesh_Component")]
        public ModelStruct Type_Model { get; set; }

        [YAXDontSerializeIfNull]
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "Animation")]
        public AsyncObservableCollection<Type0> Type_0 { get; set; }
        [YAXDontSerializeIfNull]
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "Animation_SubGroup")]
        public AsyncObservableCollection<Type1_Header> Type_1 { get; set; }

        //SubEntry
        [YAXDontSerializeIfNull]
        [YAXCollection(YAXCollectionSerializationTypes.Recursive, EachElementName = "ParticleEffect")]
        public AsyncObservableCollection<ParticleEffect> ChildParticleEffects { get; set; }

        public enum AutoOrientationType
        {
            Camera = 0,
            Front = 1
        }

        [YAXDontSerialize]
        public IEnumerable<AutoOrientationType> AutoOrientationTypes
        {
            get
            {
                return Enum.GetValues(typeof(AutoOrientationType))
                    .Cast<AutoOrientationType>();
            }
        }

        public static ComponentType GetComponentType(int[] flags)
        {
            if (flags[1] == 2)
            {
                switch (flags[0])
                {
                    case 0:
                        return ComponentType.AutoOriented;
                    case 1:
                        return ComponentType.AutoOriented_VisibleOnSpeed;
                    case 2:
                        return ComponentType.Default;
                    case 3:
                        return ComponentType.ConeExtrude;
                    case 4:
                        return ComponentType.Mesh;
                    case 5:
                        return ComponentType.ShapeDraw;
                    default:
                        return ComponentType.None;

                }
            }
            else if (flags[1] == 1)
            {
                switch (flags[0])
                {
                    case 0:
                        return ComponentType.VerticalDistribution;
                    case 1:
                        return ComponentType.SphericalDistribution;
                    case 2:
                        return ComponentType.ShapePerimeterDistribution;
                    case 3:
                        return ComponentType.ShapeAreaDistribution;
                    default:
                        return ComponentType.None;

                }
            }
            else
            {
                return ComponentType.None;
            }
        }

        public string GetDataFlagsFromDeclaredType()
        {
            switch (Component_Type)
            {
                case ComponentType.AutoOriented:
                    return "00_02";
                case ComponentType.AutoOriented_VisibleOnSpeed:
                    return "01_02";
                case ComponentType.Default:
                    return "02_02";
                case ComponentType.ConeExtrude:
                    return "03_02";
                case ComponentType.Mesh:
                    return "04_02";
                case ComponentType.ShapeDraw:
                    return "05_02";
                case ComponentType.VerticalDistribution:
                    return "00_01";
                case ComponentType.SphericalDistribution:
                    return "01_01";
                case ComponentType.ShapePerimeterDistribution:
                    return "02_01";
                case ComponentType.ShapeAreaDistribution:
                    return "03_01";
                case ComponentType.None:
                    return "00_00";
                default:
                    return "00_00";
            }
        }

        public bool IsScale2Enabled()
        {
            return I_33_4;
        }

        public ParticleEffect Clone(bool ignoreChildren = false)
        {
            AsyncObservableCollection<Type0> _type0 = new AsyncObservableCollection<Type0>();
            AsyncObservableCollection<Type1_Header> _type1 = new AsyncObservableCollection<Type1_Header>();
            AsyncObservableCollection<ParticleEffect> _children = new AsyncObservableCollection<ParticleEffect>();

            if(Type_0 != null)
            {
                foreach (var e in Type_0)
                {
                    _type0.Add(e.Clone());
                }
            }

            if(Type_1 != null)
            {
                foreach (var e in Type_1)
                {
                    _type1.Add(e.Clone());
                }
            }

            if (!ignoreChildren && ChildParticleEffects != null)
            {
                foreach (var e in ChildParticleEffects)
                {
                    _children.Add(e.Clone());
                }
            }

            return new ParticleEffect()
            {
                I_136 = I_136,
                I_32_0 = I_32_0,
                I_32_1 = I_32_1,
                I_32_2 = I_32_2,
                I_32_3 = I_32_3,
                I_32_4 = I_32_4,
                I_32_5 = I_32_5,
                I_32_6 = I_32_6,
                I_32_7 = I_32_7,
                I_33_0 = I_33_0,
                I_33_1 = I_33_1,
                I_33_2 = I_33_2,
                I_33_3 = I_33_3,
                I_33_4 = I_33_4,
                I_33_5 = I_33_5,
                I_33_6 = I_33_6,
                I_33_7 = I_33_7,
                I_34_0 = I_34_0,
                I_34_1 = I_34_1,
                I_34_2 = I_34_2,
                I_34_3 = I_34_3,
                I_34_4 = I_34_4,
                I_34_5 = I_34_5,
                I_34_6 = I_34_6,
                I_34_7 = I_34_7,
                I_35 = I_35,
                I_38 = I_38,
                I_40 = I_40,
                I_42 = I_42,
                I_44 = I_44,
                I_45 = I_45,
                I_46 = I_46,
                I_47 = I_47,
                I_48 = I_48,
                I_50 = I_50,
                I_52 = I_52,
                I_54 = I_54,
                I_56 = I_56,
                I_58 = I_58,
                I_60 = I_60,
                I_62 = I_62,
                F_100 = F_100,
                F_104 = F_104,
                F_108 = F_108,
                F_112 = F_112,
                F_116 = F_116,
                F_120 = F_120,
                F_124 = F_124,
                F_128 = F_128,
                F_132 = F_132,
                F_64 = F_64,
                F_68 = F_68,
                F_72 = F_72,
                F_76 = F_76,
                F_80 = F_80,
                F_84 = F_84,
                F_88 = F_88,
                F_92 = F_92,
                F_96 = F_96,
                Component_Type = Component_Type,
                FloatPart_00_01 = FloatPart_00_01.Clone(),
                FloatPart_00_02 = FloatPart_00_02.Clone(),
                FloatPart_01_01 = FloatPart_01_01.Clone(),
                FloatPart_02_01 = FloatPart_02_01.Clone(),
                FloatPart_02_02 = FloatPart_02_02.Clone(),
                FloatPart_03_01 = FloatPart_03_01.Clone(),
                Name = Utils.CloneString(Name),
                Type_0 = _type0,
                Type_1 = Type_1,
                Type_Model = Type_Model.Clone(),
                Type_Struct3 = Type_Struct3.Clone(),
                Type_Struct5 = Type_Struct5.Clone(),
                Type_Texture = Type_Texture.Clone(),
                ChildParticleEffects = _children
            };
        }

        public static ParticleEffect GetNew(bool initComponents = true)
        {
            if (initComponents)
            {
                return new ParticleEffect()
                {
                    Name = "New ParticleEffect",
                    Component_Type = ComponentType.None,
                    ChildParticleEffects = new AsyncObservableCollection<ParticleEffect>(),
                    Type_Texture = TexturePart.GetNew(),
                    FloatPart_00_01 = FloatPart_0_1.GetNew(),
                    FloatPart_00_02 = FloatPart_0_2.GetNew(),
                    FloatPart_01_01 = FloatPart_1_1.GetNew(),
                    FloatPart_02_01 = FloatPart_2_1.GetNew(),
                    FloatPart_02_02 = FloatPart_2_2.GetNew(),
                    FloatPart_03_01 = FloatPart_3_1.GetNew(),
                    Type_Model = ModelStruct.GetNew(),
                    Type_Struct3 = Struct3.GetNew(),
                    Type_Struct5 = Struct5.GetNew(),
                    I_35 = AutoOrientationType.Camera,
                    Type_0 = new AsyncObservableCollection<Type0>(),
                    Type_1 = new AsyncObservableCollection<Type1_Header>()
                };
            }
            else
            {
                return new ParticleEffect()
                {
                    Name = "New ParticleEffect",
                    Component_Type = ComponentType.None,
                    I_35 = AutoOrientationType.Camera
                };
            }
            
        }

        /// <summary>
        /// Add a new ParticleEffect entry.
        /// </summary>
        /// <param name="index">Where in the collection to insert the new entry. The default value of -1 will result in it being added to the end.</param>
        public void AddNew(int index = -1, List<IUndoRedo> undos = null)
        {
            if (index < -1 || index > ChildParticleEffects.Count() - 1) index = -1;

            var newEffect = GetNew();

            if (index == -1)
            {
                ChildParticleEffects.Add(newEffect);

                if (undos != null)
                    undos.Add(new UndoableListAdd<ParticleEffect>(ChildParticleEffects, newEffect));
            }
            else
            {
                ChildParticleEffects.Insert(index, newEffect);

                if (undos != null)
                    undos.Add(new UndoableStateChange<ParticleEffect>(ChildParticleEffects, index, ChildParticleEffects[index], newEffect));
            }
        }

        public bool IsTextureType()
        {
            ParticleEffect particle = this;
            if (particle.Component_Type == ParticleEffect.ComponentType.AutoOriented_VisibleOnSpeed ||
                    particle.Component_Type == ParticleEffect.ComponentType.AutoOriented ||
                    particle.Component_Type == ParticleEffect.ComponentType.ConeExtrude ||
                    particle.Component_Type == ParticleEffect.ComponentType.Default ||
                    particle.Component_Type == ParticleEffect.ComponentType.Mesh ||
                    particle.Component_Type == ParticleEffect.ComponentType.ShapeDraw)
            {
                return true;
            }
            return false;
        }

        public void CopyValues(ParticleEffect particleEffect, List<IUndoRedo> undos = null)
        {
            if (undos == null) undos = new List<IUndoRedo>();

            undos.AddRange(Utils.CopyValues(this, particleEffect));

            var typeModel = particleEffect.Type_Model.Copy();
            var floatPart00_01 = particleEffect.FloatPart_00_01.Copy();
            var floatPart00_02 = particleEffect.FloatPart_00_02.Copy();
            var floatPart01_01 = particleEffect.FloatPart_01_01.Copy();
            var floatPart02_01 = particleEffect.FloatPart_02_01.Copy();
            var floatPart02_02 = particleEffect.FloatPart_02_02.Copy();
            var floatPart_03_01 = particleEffect.FloatPart_03_01.Copy();
            var type_Struct3 = particleEffect.Type_Struct3.Copy();
            var type_Struct5 = particleEffect.Type_Struct5.Copy();
            var type_Texture = particleEffect.Type_Texture.Copy();
            var type_0 = particleEffect.Type_0.Copy();
            var type_1 = particleEffect.Type_1.Copy();

            this.Type_Model = particleEffect.Type_Model.Copy();
            this.FloatPart_00_01 = particleEffect.FloatPart_00_01.Copy();
            FloatPart_00_02 = particleEffect.FloatPart_00_02.Copy();
            FloatPart_01_01 = particleEffect.FloatPart_01_01.Copy();
            FloatPart_02_01 = particleEffect.FloatPart_02_01.Copy();
            FloatPart_02_02 = particleEffect.FloatPart_02_02.Copy();
            FloatPart_03_01 = particleEffect.FloatPart_03_01.Copy();
            this.Type_Struct3 = particleEffect.Type_Struct3.Copy();
            this.Type_Struct5 = particleEffect.Type_Struct5.Copy();
            this.Type_Texture = particleEffect.Type_Texture.Copy();
            this.Type_0 = particleEffect.Type_0.Copy();
            this.Type_1 = particleEffect.Type_1.Copy();

            undos.Add(new UndoableProperty<ParticleEffect>(nameof(Type_Model), this, typeModel, this.Type_Model));
            undos.Add(new UndoableProperty<ParticleEffect>(nameof(FloatPart_00_01), this, floatPart00_01, this.FloatPart_00_01));
            undos.Add(new UndoableProperty<ParticleEffect>(nameof(FloatPart_00_02), this, floatPart00_02, this.FloatPart_00_02));
            undos.Add(new UndoableProperty<ParticleEffect>(nameof(FloatPart_01_01), this, floatPart01_01, this.FloatPart_01_01));
            undos.Add(new UndoableProperty<ParticleEffect>(nameof(FloatPart_02_01), this, floatPart02_01, this.FloatPart_02_01));
            undos.Add(new UndoableProperty<ParticleEffect>(nameof(FloatPart_02_02), this, floatPart02_02, this.FloatPart_02_02));
            undos.Add(new UndoableProperty<ParticleEffect>(nameof(FloatPart_03_01), this, floatPart_03_01, this.FloatPart_03_01));
            undos.Add(new UndoableProperty<ParticleEffect>(nameof(Type_Struct3), this, type_Struct3, this.Type_Struct3));
            undos.Add(new UndoableProperty<ParticleEffect>(nameof(Type_Struct5), this, type_Struct5, this.Type_Struct5));
            undos.Add(new UndoableProperty<ParticleEffect>(nameof(Type_Texture), this, type_Texture, this.Type_Texture));
            undos.Add(new UndoableProperty<ParticleEffect>(nameof(Type_0), this, type_0, this.Type_0));
            undos.Add(new UndoableProperty<ParticleEffect>(nameof(Type_1), this, type_1, this.Type_1));

            undos.Add(new UndoActionPropNotify(this, true));

            this.NotifyPropsChanged();
        }


        /// <summary>
        /// Ensures that an animation exists for each color component.
        /// </summary>
        public void InitColor1Animations()
        {
            float defaultR = (Type_Texture != null) ? Type_Texture.Color1.R : 0f;
            float defaultG = (Type_Texture != null) ? Type_Texture.Color1.G : 0f;
            float defaultB = (Type_Texture != null) ? Type_Texture.Color1.B : 0f;

            if (Type_0 == null) return;

            var r = GetColorAnimation(Type0.ComponentColor1.R);
            var g = GetColorAnimation(Type0.ComponentColor1.G);
            var b = GetColorAnimation(Type0.ComponentColor1.B);

            //Create missing animations
            if(r != null || g != null || b != null)
            {
                if(r == null)
                {
                    var newR = Type0.GetNew(defaultR);
                    newR.SelectedComponentColor1 = Type0.ComponentColor1.R;
                    newR.SelectedComponentColor2 = Type0.ComponentColor2.R;
                    newR.SelectedParameter = Type0.Parameter.Color1;
                    Type_0.Add(newR);
                } 

                if (g == null)
                {
                    var newG = Type0.GetNew(defaultG);
                    newG.SelectedComponentColor1 = Type0.ComponentColor1.G;
                    newG.SelectedComponentColor2 = Type0.ComponentColor2.G;
                    newG.SelectedParameter = Type0.Parameter.Color1;
                    Type_0.Add(newG);
                }

                if (b == null)
                {
                    var newB = Type0.GetNew(defaultB);
                    newB.SelectedComponentColor1 = Type0.ComponentColor1.B;
                    newB.SelectedComponentColor2 = Type0.ComponentColor2.B;
                    newB.SelectedParameter = Type0.Parameter.Color1;
                    Type_0.Add(newB);
                }
                
            }

            r = GetColorAnimation(Type0.ComponentColor1.R);
            g = GetColorAnimation(Type0.ComponentColor1.G);
            b = GetColorAnimation(Type0.ComponentColor1.B);

            //Add keyframe at frame 0, if it doesn't exist
            if (r != null && g != null && b != null)
            {
                if (r.GetKeyframe(0) == null)
                {
                    r.SetValue(0, defaultR);
                }

                if (g.GetKeyframe(0) == null)
                {
                    g.SetValue(0, defaultG);
                }

                if (b.GetKeyframe(0) == null)
                {
                    b.SetValue(0, defaultB);
                }
            }

            //Add missing keyframes
            foreach (var anim in Type_0)
            {
                foreach(var anim2 in Type_0)
                {
                    if(anim.SelectedParameter == Type0.Parameter.Color1 && anim2.SelectedParameter == Type0.Parameter.Color1 && !anim.IsAlpha && !anim2.IsAlpha)
                    {
                        anim.AddKeyframesFromAnim(anim2);
                    }
                }
            }
        }

        public void InitColor2Animations()
        {
            float defaultR = (Type_Texture != null) ? Type_Texture.Color2.R : 0f;
            float defaultG = (Type_Texture != null) ? Type_Texture.Color2.G : 0f;
            float defaultB = (Type_Texture != null) ? Type_Texture.Color2.B : 0f;

            if (Type_0 == null) return;

            var r = GetColorAnimation(Type0.ComponentColor2.R);
            var g = GetColorAnimation(Type0.ComponentColor2.G);
            var b = GetColorAnimation(Type0.ComponentColor2.B);

            //Create missing animations
            if (r != null || g != null || b != null)
            {
                if (r == null)
                {
                    var newR = Type0.GetNew(defaultR);
                    newR.SelectedComponentColor2 = Type0.ComponentColor2.R;
                    newR.SelectedParameter = Type0.Parameter.Color2;
                    Type_0.Add(newR);
                }

                if (g == null)
                {
                    var newG = Type0.GetNew(defaultG);
                    newG.SelectedComponentColor2 = Type0.ComponentColor2.G;
                    newG.SelectedParameter = Type0.Parameter.Color2;
                    Type_0.Add(newG);
                }

                if (b == null)
                {
                    var newB = Type0.GetNew(defaultB);
                    newB.SelectedComponentColor2 = Type0.ComponentColor2.B;
                    newB.SelectedParameter = Type0.Parameter.Color2;
                    Type_0.Add(newB);
                }
                
            }

            r = GetColorAnimation(Type0.ComponentColor2.R);
            g = GetColorAnimation(Type0.ComponentColor2.G);
            b = GetColorAnimation(Type0.ComponentColor2.B);

            if (r != null && g != null && b != null)
            {

                //Add keyframe at frame 0, if it doesn't exist
                if (r.GetKeyframe(0) == null)
                {
                    r.SetValue(0, defaultR);
                }

                if (g.GetKeyframe(0) == null)
                {
                    g.SetValue(0, defaultG);
                }

                if (b.GetKeyframe(0) == null)
                {
                    b.SetValue(0, defaultB);
                }
            }



            //Add missing keyframes
            foreach (var anim in Type_0)
            {
            foreach (var anim2 in Type_0)
            {
                if (anim.SelectedParameter == Type0.Parameter.Color2 && anim2.SelectedParameter == Type0.Parameter.Color2 && !anim.IsAlpha && !anim2.IsAlpha)
                {
                    anim.AddKeyframesFromAnim(anim2);
                }
            }
        }
        }


        public Type0 GetColorAnimation(Type0.ComponentColor1 component)
        {
            foreach(var anim in Type_0)
            {
                if(anim.SelectedParameter == Type0.Parameter.Color1 && anim.SelectedComponentColor1 == component)
                {
                    return anim;
                }
            }

            return null;
        }

        public Type0 GetColorAnimation(Type0.ComponentColor2 component)
        {
            foreach (var anim in Type_0)
            {
                if (anim.SelectedParameter == Type0.Parameter.Color2 && anim.SelectedComponentColor2 == component)
                {
                    return anim;
                }
            }

            return null;
        }

        public List<RgbColor> GetUsedColors()
        {
            List<RgbColor> colors = new List<RgbColor>();

            if (Type_Texture != null)
            {
                if (!Type_Texture.Color1.IsWhiteOrBlack())
                {
                    colors.Add(new RgbColor(Type_Texture.Color1));
                }

                if (!Type_Texture.Color2.IsWhiteOrBlack())
                {
                    colors.Add(new RgbColor(Type_Texture.Color2));
                }
            }

            if (Type_0 != null)
            {
                InitColor1Animations();
                InitColor2Animations();

                //Color 1
                Type0 color1_R = Type_0.FirstOrDefault(x => x.SelectedParameter == Type0.Parameter.Color1 && x.SelectedComponentColor1 == Type0.ComponentColor1.R);
                Type0 color1_G = Type_0.FirstOrDefault(x => x.SelectedParameter == Type0.Parameter.Color1 && x.SelectedComponentColor1 == Type0.ComponentColor1.G);
                Type0 color1_B = Type_0.FirstOrDefault(x => x.SelectedParameter == Type0.Parameter.Color1 && x.SelectedComponentColor1 == Type0.ComponentColor1.B);

                if(color1_R != null && color1_G != null && color1_B != null)
                {
                    foreach(var r_Keyframe in color1_R.Keyframes)
                    {
                        Type0_Keyframe g_Keyframe = color1_G.GetKeyframe(r_Keyframe.Index);
                        Type0_Keyframe b_Keyframe = color1_B.GetKeyframe(r_Keyframe.Index);

                        if(g_Keyframe != null && b_Keyframe != null)
                        {
                            var newColor = new RgbColor(r_Keyframe.Float, g_Keyframe.Float, b_Keyframe.Float);

                            if(!newColor.IsWhiteOrBlack)
                                colors.Add(newColor);
                        }
                    }
                }

                //Color 2
                Type0 color2_R = Type_0.FirstOrDefault(x => x.SelectedParameter == Type0.Parameter.Color2 && x.SelectedComponentColor2 == Type0.ComponentColor2.R);
                Type0 color2_G = Type_0.FirstOrDefault(x => x.SelectedParameter == Type0.Parameter.Color2 && x.SelectedComponentColor2 == Type0.ComponentColor2.G);
                Type0 color2_B = Type_0.FirstOrDefault(x => x.SelectedParameter == Type0.Parameter.Color2 && x.SelectedComponentColor2 == Type0.ComponentColor2.B);

                if (color2_R != null && color2_G != null && color2_B != null)
                {
                    foreach (var r_Keyframe in color2_R.Keyframes)
                    {
                        Type0_Keyframe g_Keyframe = color2_G.GetKeyframe(r_Keyframe.Index);
                        Type0_Keyframe b_Keyframe = color2_B.GetKeyframe(r_Keyframe.Index);

                        if (g_Keyframe != null && b_Keyframe != null)
                        {
                            var newColor = new RgbColor(r_Keyframe.Float, g_Keyframe.Float, b_Keyframe.Float);

                            if (!newColor.IsWhiteOrBlack)
                                colors.Add(newColor);
                        }
                    }
                }

            }

            if(ChildParticleEffects != null)
            {
                foreach(var child in ChildParticleEffects)
                {
                    colors.AddRange(child.GetUsedColors());
                }
            }

            return colors;
        }

        public void ChangeHue(double hue, double saturation, double lightness, List<IUndoRedo> undos, bool hueSet = false, int variance = 0)
        {
            if(Type_Texture != null)
            {
                //If its all 255 or 0, then skip the color
                if (!Type_Texture.Color1.IsWhiteOrBlack())
                {
                    HslColor.HslColor newCol1 = new RgbColor(Type_Texture.Color1).ToHsl();
                    RgbColor convertedColor;

                    if (hueSet)
                    {
                        newCol1.SetHue(hue, variance);
                    }
                    else
                    {
                        newCol1.ChangeHue(hue);
                        newCol1.ChangeSaturation(saturation);
                        newCol1.ChangeLightness(lightness);
                    }

                    convertedColor = newCol1.ToRgb();

                    undos.Add(new UndoableProperty<TexturePart>(nameof(Type_Texture.Color1.R), Type_Texture.Color1, Type_Texture.Color1.R, (float)convertedColor.R));
                    undos.Add(new UndoableProperty<TexturePart>(nameof(Type_Texture.Color1.G), Type_Texture.Color1, Type_Texture.Color1.G, (float)convertedColor.G));
                    undos.Add(new UndoableProperty<TexturePart>(nameof(Type_Texture.Color1.B), Type_Texture.Color1, Type_Texture.Color1.B, (float)convertedColor.B));

                    Type_Texture.Color1.R = (float)convertedColor.R;
                    Type_Texture.Color1.G = (float)convertedColor.G;
                    Type_Texture.Color1.B = (float)convertedColor.B;
                }

                //If its all 255 or 0, then skip the color
                if (!Type_Texture.Color2.IsWhiteOrBlack())
                {
                    HslColor.HslColor newCol2 = new RgbColor(Type_Texture.Color2).ToHsl();
                    RgbColor convertedColor;

                    if (hueSet)
                    {
                        newCol2.SetHue(hue, variance);
                    }
                    else
                    {
                        newCol2.ChangeHue(hue);
                        newCol2.ChangeSaturation(saturation);
                        newCol2.ChangeLightness(lightness);
                    }

                    convertedColor = newCol2.ToRgb();

                    undos.Add(new UndoableProperty<TexturePart>(nameof(Type_Texture.Color2.R), Type_Texture.Color2, Type_Texture.Color2.R, (float)convertedColor.R));
                    undos.Add(new UndoableProperty<TexturePart>(nameof(Type_Texture.Color2.G), Type_Texture.Color2, Type_Texture.Color2.G, (float)convertedColor.G));
                    undos.Add(new UndoableProperty<TexturePart>(nameof(Type_Texture.Color2.B), Type_Texture.Color2, Type_Texture.Color2.B, (float)convertedColor.B));

                    Type_Texture.Color2.R = (float)convertedColor.R;
                    Type_Texture.Color2.G = (float)convertedColor.G;
                    Type_Texture.Color2.B = (float)convertedColor.B;
                }

                //Random Range. Change it if any of the values aren't 0.
                if(Type_Texture.Color_Variance != 0f)
                {
                    RgbColor convertedColor;

                    if (hueSet)
                    {
                        //Remove random range in Hue Set mode
                        convertedColor = new RgbColor(0f, 0f, 0f);
                    }
                    else
                    {
                        HslColor.HslColor newCol = new RgbColor(Type_Texture.Color_Variance).ToHsl();
                        newCol.ChangeHue(hue);
                        newCol.ChangeSaturation(saturation);
                        newCol.ChangeLightness(lightness);
                        convertedColor = newCol.ToRgb();
                    }


                    undos.Add(new UndoableProperty<TexturePart>(nameof(Type_Texture.Color_Variance.R), Type_Texture.Color_Variance, Type_Texture.Color_Variance.R, (float)convertedColor.R));
                    undos.Add(new UndoableProperty<TexturePart>(nameof(Type_Texture.Color_Variance.G), Type_Texture.Color_Variance, Type_Texture.Color_Variance.G, (float)convertedColor.G));
                    undos.Add(new UndoableProperty<TexturePart>(nameof(Type_Texture.Color_Variance.B), Type_Texture.Color_Variance, Type_Texture.Color_Variance.B, (float)convertedColor.B));

                    Type_Texture.Color_Variance.R = (float)convertedColor.R;
                    Type_Texture.Color_Variance.G = (float)convertedColor.G;
                    Type_Texture.Color_Variance.B = (float)convertedColor.B;
                }

            }
        
            if(Type_0 != null)
            {
                ChangeHueForColor1Animations(hue, saturation, lightness, undos, hueSet, variance);
                ChangeHueForColor2Animations(hue, saturation, lightness, undos, hueSet, variance);
            }


            //Children
            if (ChildParticleEffects != null)
            {
                foreach (var child in ChildParticleEffects)
                {
                    child.ChangeHue(hue, saturation, lightness, undos, hueSet);
                }
            }
        }

        private void ChangeHueForColor1Animations(double hue, double saturation, double lightness, List<IUndoRedo> undos, bool hueSet = false, int variance = 0)
        {
            Type0 r = Type_0.FirstOrDefault(e => e.SelectedComponentColor1 == Type0.ComponentColor1.R && e.SelectedParameter == Type0.Parameter.Color1);
            Type0 g = Type_0.FirstOrDefault(e => e.SelectedComponentColor1 == Type0.ComponentColor1.G && e.SelectedParameter == Type0.Parameter.Color1);
            Type0 b = Type_0.FirstOrDefault(e => e.SelectedComponentColor1 == Type0.ComponentColor1.B && e.SelectedParameter == Type0.Parameter.Color1);
            if (r == null || g == null || b == null) return;

            foreach(var r_frame in r.Keyframes)
            {
                float g_frame = g.GetValue(r_frame.Index);
                float b_frame = b.GetValue(r_frame.Index);

                if (r_frame.Float != 0.0 || g_frame != 0.0 || b_frame != 0.0)
                {
                    HslColor.HslColor color = new RgbColor(r_frame.Float, g_frame, b_frame).ToHsl();
                    RgbColor convertedColor;

                    if (hueSet)
                    {
                        color.SetHue(hue, variance);
                    }
                    else
                    {
                        color.ChangeHue(hue);
                        color.ChangeSaturation(saturation);
                        color.ChangeLightness(lightness);
                    }

                    convertedColor = color.ToRgb();

                    r.SetValue(r_frame.Index, (float)convertedColor.R, undos);
                    g.SetValue(r_frame.Index, (float)convertedColor.G, undos);
                    b.SetValue(r_frame.Index, (float)convertedColor.B, undos);
                }
            }

        }

        private void ChangeHueForColor2Animations(double hue, double saturation, double lightness, List<IUndoRedo> undos, bool hueSet = false, int variance = 0)
        {
            Type0 r = Type_0.FirstOrDefault(e => e.SelectedComponentColor2 == Type0.ComponentColor2.R && e.SelectedParameter == Type0.Parameter.Color2);
            Type0 g = Type_0.FirstOrDefault(e => e.SelectedComponentColor2 == Type0.ComponentColor2.G && e.SelectedParameter == Type0.Parameter.Color2);
            Type0 b = Type_0.FirstOrDefault(e => e.SelectedComponentColor2 == Type0.ComponentColor2.B && e.SelectedParameter == Type0.Parameter.Color2);

            if (r == null || g == null || b == null) return;

            foreach (var r_frame in r.Keyframes)
            {
                float g_frame = g.GetValue(r_frame.Index);
                float b_frame = b.GetValue(r_frame.Index);

                if (r_frame.Float != 0.0 || g_frame != 0.0 || b_frame != 0.0)
                {
                    HslColor.HslColor color = new RgbColor(r_frame.Float, g_frame, b_frame).ToHsl();
                    RgbColor convertedColor;

                    if (hueSet)
                    {
                        color.SetHue(hue, variance);
                    }
                    else
                    {
                        color.ChangeHue(hue);
                        color.ChangeSaturation(saturation);
                        color.ChangeLightness(lightness);
                    }

                    convertedColor = color.ToRgb();

                    r.SetValue(r_frame.Index, (float)convertedColor.R, undos);
                    g.SetValue(r_frame.Index, (float)convertedColor.G, undos);
                    b.SetValue(r_frame.Index, (float)convertedColor.B, undos);
                }
            }

        }

        public void RemoveColorRandomRange(List<IUndoRedo> undos)
        {
            if(Type_Texture != null)
            {
                if (Type_Texture.Color_Variance != 0f)
                {
                    undos.Add(new UndoableProperty<TexturePart>(nameof(Type_Texture.Color_Variance.R), Type_Texture.Color_Variance, Type_Texture.Color_Variance.R, 0f));
                    undos.Add(new UndoableProperty<TexturePart>(nameof(Type_Texture.Color_Variance.G), Type_Texture.Color_Variance, Type_Texture.Color_Variance.G, 0f));
                    undos.Add(new UndoableProperty<TexturePart>(nameof(Type_Texture.Color_Variance.B), Type_Texture.Color_Variance, Type_Texture.Color_Variance.B, 0f));

                    Type_Texture.Color_Variance.R = 0f;
                    Type_Texture.Color_Variance.G = 0f;
                    Type_Texture.Color_Variance.B = 0f;
                }
            }
        }

        public void RemoveColorType0Animations(List<IUndoRedo> undos)
        {
            if (Type_0 != null)
            {
                for (int i = Type_0.Count - 1; i >= 0; i--)
                {
                    if((Type_0[i].SelectedParameter == Type0.Parameter.Color1 || Type_0[i].SelectedParameter == Type0.Parameter.Color2))
                    {
                        if (Type_0[i].SelectedParameter == Type0.Parameter.Color1 && Type_0[i].SelectedComponentColor1 == Type0.ComponentColor1.A) continue;
                        if (Type_0[i].SelectedParameter == Type0.Parameter.Color2 && Type_0[i].SelectedComponentColor2 == Type0.ComponentColor2.A) continue;

                        undos.Add(new UndoableListRemove<Type0>(Type_0, Type_0[i]));
                        Type_0.RemoveAt(i);
                    }
                }
            }
        }

        public void RemoveColorType1Animations(List<IUndoRedo> undos)
        {
            if (Type_1 != null)
            {
                foreach(var type1 in Type_1)
                {
                    for (int i = type1.Entries.Count - 1; i >= 0; i--)
                    {
                        if ((type1.Entries[i].SelectedParameter == Type0.Parameter.Color1 || type1.Entries[i].SelectedParameter == Type0.Parameter.Color2))
                        {
                            if (type1.Entries[i].SelectedParameter == Type0.Parameter.Color1 && type1.Entries[i].SelectedComponentColor1 == Type0.ComponentColor1.A) continue;
                            if (type1.Entries[i].SelectedParameter == Type0.Parameter.Color2 && type1.Entries[i].SelectedComponentColor2 == Type0.ComponentColor2.A) continue;

                            undos.Add(new UndoableListRemove<Type0>(type1.Entries, type1.Entries[i]));
                            type1.Entries.RemoveAt(i);
                        }
                    }
                }
            }
        }
    }

    //FLAG36/37 data
    [Serializable]
    [YAXSerializeAs("TexturePart")]
    public class TexturePart : INotifyPropertyChanged
    {
        #region NotifyPropertyChanged
        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        private void NotifyPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion

        #region References
        private EmmMaterial _materialRef = null;
        [YAXDontSerialize]
        public EmmMaterial MaterialRef
        {
            get
            {
                return _materialRef;
            }
            set
            {
                if (_materialRef != value)
                    _materialRef = value;
                NotifyPropertyChanged(nameof(MaterialRef));
            }
        }
        [YAXDontSerialize]
        public AsyncObservableCollection<TextureEntry_Ref> TextureEntryRef { get; set; } = new AsyncObservableCollection<TextureEntry_Ref>();
        
        #endregion

        [YAXAttributeForClass]
        [YAXCollection(YAXCollectionSerializationTypes.Serially, SeparateBy = ", ")]
        [YAXSerializeAs("Texture_ID")]
        public AsyncObservableCollection<int> TextureIndex { get; set; } //XML Mode (TextureEntryRef for Tool mode)

        [YAXAttributeFor("I_00")]
        [YAXSerializeAs("int8")]
        public byte I_00 { get; set; }
        [YAXAttributeFor("I_01")]
        [YAXSerializeAs("int8")]
        public byte I_01 { get; set; }
        [YAXAttributeFor("I_02")]
        [YAXSerializeAs("int8")]
        public byte I_02 { get; set; }
        [YAXAttributeFor("I_03")]
        [YAXSerializeAs("int8")]
        public byte I_03 { get; set; }


        [YAXAttributeFor("AddedDepth")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_04 { get; set; }
        [YAXAttributeFor("I_08")]
        [YAXSerializeAs("value")]
        public int I_08 { get; set; }
        [YAXAttributeFor("I_12")]
        [YAXSerializeAs("value")]
        public int I_12 { get; set; }
        [YAXAttributeFor("EmmMaterial")]
        [YAXSerializeAs("Index")]
        public ushort I_16 { get; set; }

        //Colors
        public CustomColor Color1 { get; set; } = new CustomColor();
        public CustomColor Color2 { get; set; } = new CustomColor();
        public CustomColor Color_Variance { get; set; } = new CustomColor();

        //Remaining floats
        [YAXAttributeFor("Scale1")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("Scale1")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_28 { get; set; }
        [YAXAttributeFor("Scale2")]
        [YAXSerializeAs("X")]
        [YAXFormat("0.0########")]
        public float F_32 { get; set; }
        [YAXAttributeFor("Scale2_AddedRandom")]
        [YAXSerializeAs("X")]
        [YAXFormat("0.0########")]
        public float F_36 { get; set; }
        [YAXAttributeFor("Scale2")]
        [YAXSerializeAs("Y")]
        [YAXFormat("0.0########")]
        public float F_40 { get; set; }
        [YAXAttributeFor("Scale2_AddedRandom")]
        [YAXSerializeAs("Y")]
        [YAXFormat("0.0########")]
        public float F_44 { get; set; }
        [YAXAttributeFor("F_96")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_96 { get; set; }
        [YAXAttributeFor("F_100")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_100 { get; set; }
        [YAXAttributeFor("F_104")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_104 { get; set; }
        [YAXAttributeFor("F_108")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_108 { get; set; }

        public TexturePart Clone()
        {
            AsyncObservableCollection<int> newTexList = AsyncObservableCollection<int>.Create();
            AsyncObservableCollection<TextureEntry_Ref> textureRefs = AsyncObservableCollection<TextureEntry_Ref>.Create();

            for (int i = 0; i < TextureIndex.Count; i++)
            {
                newTexList.Add(TextureIndex[i]);
            }

            if(TextureEntryRef != null)
            {
                foreach(var textRef in TextureEntryRef)
                {
                    textureRefs.Add(new EMP.TextureEntry_Ref() { TextureRef = textRef.TextureRef });
                }
            }

            return new TexturePart()
            {
                I_00 = I_00,
                I_01 = I_01,
                I_02 = I_02,
                I_03 = I_03,
                I_08 = I_08,
                I_12 = I_12,
                I_16 = I_16,
                F_04 = F_04,
                F_100 = F_100,
                F_104 = F_104,
                F_108 = F_108,
                F_24 = F_24,
                F_28 = F_28,
                F_32 = F_32,
                F_36 = F_36,
                F_40 = F_40,
                F_44 = F_44,
                Color1 = Color1 != null ? Color1.Copy() : new CustomColor(),
                Color_Variance = Color_Variance != null ? Color_Variance.Copy() : new CustomColor(),
                Color2 = Color2 != null ? Color2.Copy() : new CustomColor(),
                F_96 = F_96,
                TextureIndex = newTexList,
                MaterialRef = MaterialRef,
                TextureEntryRef = textureRefs
            };
        }

        public static TexturePart GetNew()
        {
            return new TexturePart()
            {
                TextureIndex = new AsyncObservableCollection<int>(),
                TextureEntryRef = new AsyncObservableCollection<TextureEntry_Ref>()
                {
                    //These are created because EMP Editor relies on there always being 2 texture references. Technically there can be any number of these, but only 2 are ever used by the game EMP files, so thats all that the EMP Editor exposes.
                    new TextureEntry_Ref(), new TextureEntry_Ref()
                }
            };
        }
        
    }

    [Serializable]
    [YAXSerializeAs("VerticalDistribution_Component")]
    public class FloatPart_0_1 : IFloatSize8
    {
        [YAXAttributeFor("PositionOffset_Y")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_00 { get; set; }
        [YAXAttributeFor("PositionOffset_Y")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_04 { get; set; }
        [YAXAttributeFor("TranslationSpeed_Y")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("TranslationSpeed_Y")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("VirtualConeOpenAngle")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("VirtualConeOpenAngle")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("F_24")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("F_28")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_28 { get; set; }

        public FloatPart_0_1 Clone()
        {
            return new FloatPart_0_1()
            {
                F_00 = F_00,
                F_04 = F_04,
                F_08 = F_08,
                F_12 = F_12,
                F_16 = F_16,
                F_20 = F_20,
                F_24 = F_24,
                F_28 = F_28
            };
        }

        public static FloatPart_0_1 GetNew()
        {
            return new FloatPart_0_1();
        }

    }

    [Serializable]
    [YAXSerializeAs("SphericalDistribution_Component")]
    public class FloatPart_1_1 : IFloatSize4
    {
        [YAXAttributeFor("SphereRadius_ForRandomPositionOnSphere")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("SphereRadius_ForRandomPositionInSphere")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("TranslationSpeed_Y")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("TranslationSpeed_Y")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_28 { get; set; }

        public FloatPart_1_1 Clone()
        {
            return new FloatPart_1_1()
            {
                F_16 = F_16,
                F_20 = F_20,
                F_24 = F_24,
                F_28 = F_28
            };
        }

        public static FloatPart_1_1 GetNew()
        {
            return new FloatPart_1_1();
        }
    }

    [Serializable]
    [YAXSerializeAs("ShapePerimeterDistribution_Component")]
    public class FloatPart_2_1
    {
        [YAXAttributeFor("PositionOffset_Y")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_00 { get; set; }
        [YAXAttributeFor("PositionOffset_Y")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_04 { get; set; }
        [YAXAttributeFor("TranslationSpeed_Y")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("TranslationSpeed_Y")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("F_16")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("F_20")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("Width1")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("Width1")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_28 { get; set; }
        [YAXAttributeFor("Width2")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_32 { get; set; }
        [YAXAttributeFor("Width2")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_36 { get; set; }
        [YAXAttributeFor("Shape")]
        [YAXSerializeAs("type")]
        public Shape I_40 { get; set; }
        [YAXAttributeFor("F_44")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_44 { get; set; }

        public enum Shape
        {
            Circle = 0,
            Square = 1
        }

        [YAXDontSerialize]
        public IEnumerable<Shape> Shapes
        {
            get
            {
                return Enum.GetValues(typeof(Shape))
                    .Cast<Shape>();
            }
        }

        public FloatPart_2_1 Clone()
        {
            return new FloatPart_2_1()
            {
                F_00 = F_00,
                F_04 = F_04,
                F_08 = F_08,
                F_12 = F_12,
                F_16 = F_16,
                F_20 = F_20,
                F_24 = F_24,
                F_28 = F_28,
                F_32 = F_32,
                F_36 = F_36,
                F_44 = F_44,
                I_40 = I_40
            };
        }

        public static FloatPart_2_1 GetNew()
        {
            return new FloatPart_2_1()
            {
                I_40 = Shape.Circle
            };
        }
    }

    [Serializable]
    [YAXSerializeAs("ShapeAreaDistribution_Component")]
    public class FloatPart_3_1
    {
        [YAXAttributeFor("PositionOffset_Y")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_00 { get; set; }
        [YAXAttributeFor("PositionOffset_Y")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_04 { get; set; }
        [YAXAttributeFor("TranslationSpeed_Y")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("TranslationSpeed_Y")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("F_16")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("F_20")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("Width1")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("Width1")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_28 { get; set; }
        [YAXAttributeFor("Width2")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_32 { get; set; }
        [YAXAttributeFor("Width2")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_36 { get; set; }
        [YAXAttributeFor("Shape")]
        [YAXSerializeAs("type")]
        public Shape I_40 { get; set; }
        [YAXAttributeFor("F_44")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0########")]
        public float F_44 { get; set; }


        public enum Shape
        {
            Circle = 0,
            Square = 1
        }

        [YAXDontSerialize]
        public IEnumerable<Shape> Shapes
        {
            get
            {
                return Enum.GetValues(typeof(Shape))
                    .Cast<Shape>();
            }
        }

        public FloatPart_3_1 Clone()
        {
            return new FloatPart_3_1()
            {
                F_00 = F_00,
                F_04 = F_04,
                F_08 = F_08,
                F_12 = F_12,
                F_16 = F_16,
                F_20 = F_20,
                F_24 = F_24,
                F_28 = F_28,
                F_32 = F_32,
                F_36 = F_36,
                F_44 = F_44,
                I_40 = I_40
            };
        }

        public static FloatPart_3_1 GetNew()
        {
            return new FloatPart_3_1()
            {
                I_40 = Shape.Circle
            };
        }

    }

    [Serializable]
    [YAXSerializeAs("AutoOriented_Component")]
    public class FloatPart_0_2 : IFloatSize4
    {
        [YAXAttributeFor("Rotation")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("Rotation")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("SpeedRotation")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("SpeedRotation")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_28 { get; set; }

        public FloatPart_0_2 Clone()
        {
            return new FloatPart_0_2()
            {
                F_16 = F_16,
                F_20 = F_20,
                F_24 = F_24,
                F_28 = F_28,
            };
        }

        public static FloatPart_0_2 GetNew()
        {
            return new FloatPart_0_2();
        }
    }

    [Serializable]
    [YAXSerializeAs("Default_Component")]
    public class FloatPart_2_2 : IFloatSize8
    {
        [YAXAttributeFor("AngleRotation")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_00 { get; set; }
        [YAXAttributeFor("AngleRotation")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_04 { get; set; }
        [YAXAttributeFor("SpeedRotation")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("SpeedRotation")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("RotationAxis")]
        [YAXSerializeAs("X")]
        [YAXFormat("0.0########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("RotationAxis")]
        [YAXSerializeAs("Y")]
        [YAXFormat("0.0########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("RotationAxis")]
        [YAXSerializeAs("Z")]
        [YAXFormat("0.0########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("RotationAxis")]
        [YAXSerializeAs("W")]
        [YAXFormat("0.0########")]
        public float F_28 { get; set; }


        public FloatPart_2_2 Clone()
        {
            return new FloatPart_2_2()
            {
                F_00 = F_00,
                F_04 = F_04,
                F_08 = F_08,
                F_12 = F_12,
                F_16 = F_16,
                F_20 = F_20,
                F_24 = F_24,
                F_28 = F_28,
            };
        }

        public static FloatPart_2_2 GetNew()
        {
            return new FloatPart_2_2();
        }
    }

    [Serializable]
    [YAXSerializeAs("ConeExtrude_Component")]
    public class Struct3
    {
        [YAXAttributeFor("Duration")]
        [YAXSerializeAs("Base")]
        public UInt16 I_00 { get; set; }
        [YAXAttributeFor("Duration")]
        [YAXSerializeAs("AddedRandom")]
        public UInt16 I_02 { get; set; }
        [YAXAttributeFor("TimeBetweenTwoStep")]
        [YAXSerializeAs("value")]
        public UInt16 I_04 { get; set; }
        [YAXAttributeFor("I_08")]
        [YAXSerializeAs("value")]
        public UInt16 I_08 { get; set; }
        [YAXAttributeFor("I_10")]
        [YAXSerializeAs("value")]
        public UInt16 I_10 { get; set; }
        [YAXCollection(YAXCollectionSerializationTypes.Recursive, EachElementName = "Point")]
        [YAXSerializeAs("Points")]
        public AsyncObservableCollection<Struct3_Entries> FloatList { get; set; }

        public Struct3 Clone()
        {
            AsyncObservableCollection<Struct3_Entries> subEntries = AsyncObservableCollection<Struct3_Entries>.Create();

            foreach (var e in FloatList)
            {
                subEntries.Add(new Struct3_Entries()
                {
                    F_00 = e.F_00,
                    F_04 = e.F_04,
                    F_08 = e.F_08,
                    F_12 = e.F_12
                });
            }

            return new Struct3()
            {
                I_00 = I_00,
                I_02 = I_02,
                I_04 = I_04,
                I_08 = I_08,
                I_10 = I_10,
                FloatList = subEntries
            };
        }

        public static Struct3 GetNew()
        {
            return new Struct3()
            {
                FloatList = AsyncObservableCollection<Struct3_Entries>.Create()
            };
        }
    }

    [Serializable]
    [YAXSerializeAs("Point")]
    public class Struct3_Entries
    {
        [YAXAttributeForClass]
        [YAXSerializeAs("WorldScaleFactor")]
        [YAXFormat("0.0###########")]
        public float F_00 { get; set; }
        [YAXAttributeForClass]
        [YAXSerializeAs("WorldScaleAdd")]
        [YAXFormat("0.0###########")]
        public float F_04 { get; set; }
        [YAXAttributeForClass]
        [YAXSerializeAs("WorldOffsetFactor")]
        [YAXFormat("0.0###########")]
        public float F_08 { get; set; }
        [YAXAttributeForClass]
        [YAXSerializeAs("WorldOffsetFactorb")]
        [YAXFormat("0.0###########")]
        public float F_12 { get; set; }

    }

    [Serializable]
    [YAXSerializeAs("ShapeDraw_Component")]
    public class Struct5
    {
        [YAXAttributeFor("Rotation")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_00 { get; set; }
        [YAXAttributeFor("Rotation")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_04 { get; set; }
        [YAXAttributeFor("AngularSpeed")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("AngularSpeed")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("AutoOrientation")]
        [YAXSerializeAs("Z_Axis")]
        public AutoOrientation I_18 { get; set; } //uint16
        [YAXAttributeFor("I_24")]
        [YAXSerializeAs("value")]
        public UInt16 I_24 { get; set; }
        [YAXAttributeFor("I_26")]
        [YAXSerializeAs("value")]
        public UInt16 I_26 { get; set; }
        [YAXAttributeFor("I_28")]
        [YAXSerializeAs("value")]
        public UInt16 I_28 { get; set; }
        [YAXAttributeFor("I_30")]
        [YAXSerializeAs("value")]
        public UInt16 I_30 { get; set; }


        [YAXCollection(YAXCollectionSerializationTypes.Recursive, EachElementName = "Point")]
        [YAXSerializeAs("BaseLine")]
        public AsyncObservableCollection<Struct5_Entries> FloatList { get; set; }

        public enum AutoOrientation
        {
            Camera = 0,
            Front = 1,
            None = 2
        }

        [YAXDontSerialize]
        public IEnumerable<AutoOrientation> AutoOrientationValues
        {
            get
            {
                return Enum.GetValues(typeof(AutoOrientation))
                    .Cast<AutoOrientation>();
            }
        }

        public Struct5 Clone()
        {
            AsyncObservableCollection<Struct5_Entries> subEntries = AsyncObservableCollection<Struct5_Entries>.Create();

            foreach (var e in FloatList)
            {
                subEntries.Add(new Struct5_Entries()
                {
                    F_00 = e.F_00,
                    F_04 = e.F_04
                });
            }

            return new Struct5()
            {
                F_00 = F_00,
                F_12 = F_12,
                F_08 = F_08,
                F_04 = F_04,
                I_18 = I_18,
                I_24 = I_24,
                I_26 = I_26,
                I_28 = I_28,
                I_30 = I_30,
                FloatList = subEntries
            };
        }

        public static Struct5 GetNew()
        {
            return new Struct5()
            {
                I_18 = AutoOrientation.Camera,
                FloatList = AsyncObservableCollection<Struct5_Entries>.Create()
            };
        }
    }

    [Serializable]
    [YAXSerializeAs("Point")]
    public class Struct5_Entries
    {
        [YAXAttributeForClass]
        [YAXSerializeAs("X")]
        [YAXFormat("0.0###########")]
        public float F_00 { get; set; }
        [YAXAttributeForClass]
        [YAXSerializeAs("Y")]
        [YAXFormat("0.0###########")]
        public float F_04 { get; set; }



    }

    [Serializable]
    [YAXSerializeAs("Mesh_Component")]
    public class ModelStruct : IFloatSize4
    {

        [YAXAttributeFor("RotateAngle")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_00 { get; set; }
        [YAXAttributeFor("RotateAngle")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_04 { get; set; }
        [YAXAttributeFor("SpeedRotation")]
        [YAXSerializeAs("Base")]
        [YAXFormat("0.0########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("SpeedRotation")]
        [YAXSerializeAs("AddedRandom")]
        [YAXFormat("0.0########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("RotationAxis")]
        [YAXSerializeAs("X")]
        [YAXFormat("0.0###########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("RotationAxis")]
        [YAXSerializeAs("Y")]
        [YAXFormat("0.0###########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("RotationAxis")]
        [YAXSerializeAs("Z")]
        [YAXFormat("0.0###########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("RotationAxis")]
        [YAXSerializeAs("W")]
        [YAXFormat("0.0###########")]
        public float F_28 { get; set; }
        [YAXAttributeFor("I_32")]
        [YAXSerializeAs("value")]
        public UInt32 I_32 { get; set; }
        [YAXAttributeFor("I_40")]
        [YAXSerializeAs("value")]
        public UInt32 I_40 { get; set; }
        [YAXAttributeFor("I_44")]
        [YAXSerializeAs("value")]
        public UInt32 I_44 { get; set; }
        [YAXAttributeFor("EmgFile")]
        [YAXSerializeAs("bytes")]
        [YAXCollection(YAXCollectionSerializationTypes.Serially, SeparateBy = ",")]
        public List<byte> emgBytes { get; set; }

        public ModelStruct Clone()
        {
            List<byte> newEmgBytes = new List<byte>();

            foreach (byte b in emgBytes)
            {
                newEmgBytes.Add(b);
            }

            return new ModelStruct()
            {
                I_32 = I_32,
                I_40 = I_40,
                I_44 = I_44,
                F_00 = F_00,
                F_04 = F_04,
                F_08 = F_08,
                F_12 = F_12,
                F_16 = F_16,
                F_20 = F_20,
                F_24 = F_24,
                F_28 = F_28,
                emgBytes = newEmgBytes
            };
        }

        public static ModelStruct GetNew()
        {
            return new ModelStruct()
            {
                emgBytes = new List<byte>()
            };
        }
    }



    //Animations

    //Type 0 data

    [Serializable]
    [YAXSerializeAs("Animation")]
    public class Type0 : INotifyPropertyChanged
    {
        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        private void NotifyPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        [YAXDontSerialize]
        public string Name
        {
            get
            {
                switch (SelectedParameterValue)
                {
                    case Parameter.Position:
                        return String.Format("{0}, {1}", SelectedParameterValue.ToString(), SelectedComponentPositionValue.ToString());
                    case Parameter.Rotation:
                        return String.Format("{0}, {1}", SelectedParameterValue.ToString(), SelectedComponentRotationValue.ToString());
                    case Parameter.Scale:
                        return String.Format("{0}, {1}", SelectedParameterValue.ToString(), SelectedComponentScaleValue.ToString());
                    case Parameter.Color1:
                        return String.Format("{0}, {1}", SelectedParameterValue.ToString(), SelectedComponentColor1Value.ToString());
                    case Parameter.Color2:
                        return String.Format("{0}, {1}", SelectedParameterValue.ToString(), SelectedComponentColor2Value.ToString());
                    default:
                        return String.Format("{0}", SelectedParameterValue.ToString());

                };
            }
        }

        [YAXDontSerialize]
        public bool IsAlpha
        {
            get
            {
                switch (SelectedParameter)
                {
                    case Parameter.Color1:
                        return (SelectedComponentColor1 == ComponentColor1.A);
                    case Parameter.Color2:
                        return (SelectedComponentColor2 == ComponentColor2.A);
                    default:
                        return false;
                }
            }
        }

        private Parameter SelectedParameterValue = Parameter.Position;
        private ComponentPosition SelectedComponentPositionValue = ComponentPosition.X;
        private ComponentRotation SelectedComponentRotationValue = ComponentRotation.X;
        private ComponentScale SelectedComponentScaleValue = ComponentScale.X;
        private ComponentColor1 SelectedComponentColor1Value = ComponentColor1.R;
        private ComponentColor2 SelectedComponentColor2Value = ComponentColor2.R;

        [YAXAttributeForClass]
        [YAXSerializeAs("Parameter")]
        public Parameter SelectedParameter
        {
            get
            {
                return this.SelectedParameterValue;
            }

            set
            {
                if (value != this.SelectedParameterValue)
                {
                    this.SelectedParameterValue = value;
                    NotifyPropertyChanged("SelectedParameter");
                    NotifyPropertyChanged("Name");
                    NotifyPropertyChanged("IsPositionVisible");
                    NotifyPropertyChanged("IsRotationVisible");
                    NotifyPropertyChanged("IsScaleVisible");
                    NotifyPropertyChanged("IsColor1Visible");
                    NotifyPropertyChanged("IsColor2Visible");
                }
            }
        }

        [YAXDontSerialize]
        public ComponentPosition SelectedComponentPosition
        {
            get
            {
                return this.SelectedComponentPositionValue;
            }

            set
            {
                if (value != this.SelectedComponentPositionValue)
                {
                    this.SelectedComponentPositionValue = value;
                    NotifyPropertyChanged("SelectedComponentPosition");
                    NotifyPropertyChanged("Name");
                }
            }
        }
        [YAXDontSerialize]
        public ComponentRotation SelectedComponentRotation
        {
            get
            {
                return this.SelectedComponentRotationValue;
            }

            set
            {
                if (value != this.SelectedComponentRotationValue)
                {
                    this.SelectedComponentRotationValue = value;
                    NotifyPropertyChanged("SelectedComponentRotation");
                    NotifyPropertyChanged("Name");
                }
            }
        }
        [YAXDontSerialize]
        public ComponentScale SelectedComponentScale
        {
            get
            {
                return this.SelectedComponentScaleValue;
            }

            set
            {
                if (value != this.SelectedComponentScaleValue)
                {
                    this.SelectedComponentScaleValue = value;
                    NotifyPropertyChanged("SelectedComponentScale");
                    NotifyPropertyChanged("Name");
                }
            }
        }
        [YAXDontSerialize]
        public ComponentColor1 SelectedComponentColor1
        {
            get
            {
                return this.SelectedComponentColor1Value;
            }

            set
            {
                if (value != this.SelectedComponentColor1Value)
                {
                    this.SelectedComponentColor1Value = value;
                    NotifyPropertyChanged("SelectedComponentColor1");
                    NotifyPropertyChanged("Name");
                }
            }
        }
        [YAXDontSerialize]
        public ComponentColor2 SelectedComponentColor2
        {
            get
            {
                return this.SelectedComponentColor2Value;
            }

            set
            {
                if (value != this.SelectedComponentColor2Value)
                {
                    this.SelectedComponentColor2Value = value;
                    NotifyPropertyChanged("SelectedComponentColor2");
                    NotifyPropertyChanged("Name");
                }
            }
        }

        //This is where the SelectedComponent is saved to and read from xml. For all other purposes, use the above properties.
        [YAXAttributeForClass]
        [YAXSerializeAs("Component")]
        public string SelectedComponent
        {
            get
            {
                switch (SelectedParameter)
                {
                    case Parameter.Color1:
                        return SelectedComponentColor1.ToString();
                    case Parameter.Color2:
                        return SelectedComponentColor2.ToString();
                    case Parameter.Scale:
                        return SelectedComponentScale.ToString();
                    case Parameter.Rotation:
                        return SelectedComponentRotation.ToString();
                    case Parameter.Position:
                        return SelectedComponentPosition.ToString();
                    default:
                        throw new InvalidOperationException("Cannot get SelectedComponent for Parameter: " + SelectedParameter);
                }
            }
            set
            {
                switch (SelectedParameter)
                {
                    case Parameter.Color1:
                        SelectedComponentColor1 = (ComponentColor1)Enum.Parse(typeof(ComponentColor1), value);
                        break;
                    case Parameter.Color2:
                        SelectedComponentColor2 = (ComponentColor2)Enum.Parse(typeof(ComponentColor2), value);
                        break;
                    case Parameter.Scale:
                        SelectedComponentScale = (ComponentScale)Enum.Parse(typeof(ComponentScale), value);
                        break;
                    case Parameter.Position:
                        SelectedComponentPosition = (ComponentPosition)Enum.Parse(typeof(ComponentPosition), value);
                        break;
                    case Parameter.Rotation:
                        SelectedComponentRotation = (ComponentRotation)Enum.Parse(typeof(ComponentRotation), value);
                        break;
                    default:
                        throw new InvalidOperationException("Cannot set SelectedComponent for Parameter: " + SelectedParameter);
                }
            }
        }

        [YAXAttributeFor("Properties")]
        [YAXSerializeAs("Interpolated")]
        public bool I_01_b { get; set; }
        [YAXAttributeFor("Properties")]
        [YAXSerializeAs("Looped")]
        public bool I_02 { get; set; }
        [YAXAttributeFor("I_03")]
        [YAXSerializeAs("int8")]
        public byte I_03 { get; set; }
        [YAXAttributeFor("F_04")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0#######")]
        public float F_04 { get; set; }
        [YAXAttributeForClass]
        [YAXSerializeAs("Duration")]
        public short I_08 { get; set; }

        [YAXDontSerialize]
        private AsyncObservableCollection<Type0_Keyframe> _keyframes = null;

        [YAXCollection(YAXCollectionSerializationTypes.Recursive, EachElementName = "Keyframe")]
        public AsyncObservableCollection<Type0_Keyframe> Keyframes
        {
            get
            {
                return this._keyframes;
            }
            set
            {
                if (value != this._keyframes)
                {
                    this._keyframes = value;
                    NotifyPropertyChanged(nameof(Keyframes));
                }
            }
        }

        //Parameter > Component (changes based on selected Parameter)
        //There are only 5 Parameters (tested all files)
        //There are only 4 unique values for Component (tested all files)
        //Type1 Parameter values = 0, 1, 2
        //Type1 Component values = 0, 1, 2
        public enum Parameter
        {
            Position = 0,
            Rotation = 1,
            Scale = 2,
            Color1 = 3,
            Color2 = 4
        }

        public enum ComponentPosition
        {
            X = 0,
            Y = 1,
            Z = 2,
            W = 3
        }

        public enum ComponentRotation
        {
            X = 0,
            Y = 1,
            Z = 2,
            W = 3
        }

        public enum ComponentScale
        {
            X = 0,
            Y = 1,
            Z = 2,
            W = 3
        }

        public enum ComponentColor1
        {
            R = 0,
            G = 1,
            B = 2,
            A = 3
        }

        public enum ComponentColor2
        {
            R = 0,
            G = 1,
            B = 2,
            A = 3
        }

        [YAXDontSerialize]
        public IEnumerable<Parameter> Parameters
        {
            get
            {
                return Enum.GetValues(typeof(Parameter))
                    .Cast<Parameter>();
            }
        }

        [YAXDontSerialize]
        public IEnumerable<ComponentPosition> ComponentPositions
        {
            get
            {
                return Enum.GetValues(typeof(ComponentPosition))
                    .Cast<ComponentPosition>();
            }
        }

        [YAXDontSerialize]
        public IEnumerable<ComponentRotation> ComponentRotations
        {
            get
            {
                return Enum.GetValues(typeof(ComponentRotation))
                    .Cast<ComponentRotation>();
            }
        }

        [YAXDontSerialize]
        public IEnumerable<ComponentScale> ComponentScales
        {
            get
            {
                return Enum.GetValues(typeof(ComponentScale))
                    .Cast<ComponentScale>();
            }
        }

        [YAXDontSerialize]
        public IEnumerable<ComponentColor1> ComponentColor1s
        {
            get
            {
                return Enum.GetValues(typeof(ComponentColor1))
                    .Cast<ComponentColor1>();
            }
        }

        [YAXDontSerialize]
        public IEnumerable<ComponentColor2> ComponentColor2s
        {
            get
            {
                return Enum.GetValues(typeof(ComponentColor2))
                    .Cast<ComponentColor2>();
            }
        }

        [YAXDontSerialize]
        public Visibility IsPositionVisible
        {
            get { return (SelectedParameter == Parameter.Position) ? Visibility.Visible : Visibility.Hidden; }
        }

        [YAXDontSerialize]
        public Visibility IsRotationVisible
        {
            get { return (SelectedParameter == Parameter.Rotation) ? Visibility.Visible : Visibility.Hidden; }
        }

        [YAXDontSerialize]
        public Visibility IsScaleVisible
        {
            get { return (SelectedParameter == Parameter.Scale) ? Visibility.Visible : Visibility.Hidden; }
        }

        [YAXDontSerialize]
        public Visibility IsColor1Visible
        {
            get { return (SelectedParameter == Parameter.Color1) ? Visibility.Visible : Visibility.Hidden; }
        }

        [YAXDontSerialize]
        public Visibility IsColor2Visible
        {
            get { return (SelectedParameter == Parameter.Color2) ? Visibility.Visible : Visibility.Hidden; }
        }


        public void SetParameters(int parameter, int component, bool scale2)
        {
            switch (parameter)
            {
                case 0:
                    SelectedParameter = Parameter.Position;
                    SelectedComponentPosition = (ComponentPosition)component;
                    break;
                case 1:
                    SelectedParameter = Parameter.Rotation;
                    SelectedComponentRotation = (ComponentRotation)component;
                    break;
                case 2:
                    SelectedParameter = Parameter.Scale;
                    SelectedComponentScale = (ComponentScale)component;
                    break;
                case 3:
                    SelectedParameter = Parameter.Color1;
                    SelectedComponentColor1 = (ComponentColor1)component;
                    break;
                case 4:
                    SelectedParameter = Parameter.Color2;
                    SelectedComponentColor2 = (ComponentColor2)component;
                    break;
            }
        }

        /// <summary>
        /// Returns the Parameter, Component and Looped value as an Int16 for writing a binary EMP file.
        /// </summary>
        public short GetParameters(bool scale2)
        {
            byte _I_00 = (byte)SelectedParameter;
            byte _I_01_a = 0;
            byte _I_01 = 0;

            switch (SelectedParameter)
            {
                case Parameter.Position:
                    _I_01_a = (byte)SelectedComponentPosition;
                    break;
                case Parameter.Rotation:
                    _I_01_a = (byte)SelectedComponentRotation;
                    break;
                case Parameter.Scale:
                    _I_01_a = (byte)SelectedComponentScale;
                    break;
                case Parameter.Color1:
                    _I_01_a = (byte)SelectedComponentColor1;
                    break;
                case Parameter.Color2:
                    _I_01_a = (byte)SelectedComponentColor2;
                    break;
            }

            _I_01 = Int4Converter.GetByte(_I_01_a, BitConverter_Ex.GetBytes(I_01_b), "Type0_Animation: Component", "Type0_Animation: Interpolated");

            return BitConverter.ToInt16(new byte[2] { _I_00, _I_01 }, 0);
        }

        public Type0 Clone()
        {
            AsyncObservableCollection<Type0_Keyframe> _keyframes = AsyncObservableCollection<Type0_Keyframe>.Create();

            foreach (var k in Keyframes)
            {
                _keyframes.Add(new Type0_Keyframe()
                {
                    Float = k.Float,
                    Index = k.Index
                });
            }

            return new Type0()
            {
                I_01_b = I_01_b,
                I_02 = I_02,
                I_03 = I_03,
                I_08 = I_08,
                F_04 = F_04,
                Keyframes = _keyframes,
                SelectedComponentColor1Value = SelectedComponentColor1Value,
                SelectedComponentColor2Value = SelectedComponentColor2Value,
                SelectedComponentPositionValue = SelectedComponentPositionValue,
                SelectedComponentRotationValue = SelectedComponentRotationValue,
                SelectedComponentScaleValue = SelectedComponentScaleValue,
                SelectedParameterValue = SelectedParameterValue,
            };
        }

        public static Type0 GetNew(float value = 0f)
        {
            var keyframes = AsyncObservableCollection<Type0_Keyframe>.Create();
            keyframes.Add(new Type0_Keyframe()
            {
                Index = 0,
                Float = value
            });
            keyframes.Add(new Type0_Keyframe()
            {
                Index = 100,
                Float = value
            });

            return new Type0()
            {
                I_01_b = true,
                I_08 = 101,
                Keyframes = keyframes
            };
        }
        
        public void AddKeyframesFromAnim(Type0 anim)
        {
            foreach(var keyframe in anim.Keyframes)
            {
                var existing = GetKeyframe(keyframe.Index);

                if(existing == null)
                {
                    var newKeyframe = new Type0_Keyframe() { Index = keyframe.Index, Float = GetValue(keyframe.Index) };
                    Keyframes.Add(newKeyframe);
                }
            }
        }

        public Type0_Keyframe GetKeyframe(int time)
        {
            foreach(var keyframe in Keyframes)
            {
                if (keyframe.Index == time) return keyframe;
            }

            return null;
        }
        
        public float GetValue(int time)
        {
            foreach(var keyframe in Keyframes)
            {
                if (keyframe.Index == time) return keyframe.Float;
            }

            //Keyframe doesn't exist. Calculate the value.
            return CalculateKeyframeValue(time);

            //return (Keyframes.Count > 0) ? Keyframes[0].Float : 0f;//Rework this to calculate the value
        }

        public void SetValue(int time, float value, List<IUndoRedo> undos = null)
        {
            foreach (var keyframe in Keyframes)
            {
                if (keyframe.Index == time)
                {
                    float oldValue = keyframe.Float;
                    keyframe.Float = value;

                    if (undos != null)
                        undos.Add(new UndoableProperty<Type0_Keyframe>(nameof(keyframe.Float), keyframe, oldValue, value));

                    return;
                }
            }

            //Keyframe doesn't exist. Add it.
            Keyframes.Add(new Type0_Keyframe() { Index = (short)time, Float = value });
        }

        public float CalculateKeyframeValue(int time)
        {
            Type0_Keyframe before = GetKeyframeBefore(time);
            Type0_Keyframe after = GetKeyframeAfter(time);

            if(after == null)
            {
                after = new Type0_Keyframe() { Index = short.MaxValue, Float = before.Float };
            }

            if(before == null)
            {
                throw new Exception("CalculateKeyframeValue: could not locate a \"before\" keyframe to use as reference.");
            }

            //Frame difference between previous frame and the current frame (current frame is AFTER the frame we want)
            int diff = after.Index - before.Index;

            //Keyframe value difference
            float keyframe2 = after.Float - before.Float;

            //Difference between the frame we WANT and the previous frame
            int diff2 = time - before.Index;

            //Divide keyframe value difference by the keyframe time difference, and then multiply it by diff2, then add the previous keyframe value
            return (keyframe2 / diff) * diff2 + before.Float;
        }

        /// <summary>
        /// Returns the keyframe that appears just before the specified frame
        /// </summary>
        /// <returns></returns>
        public Type0_Keyframe GetKeyframeBefore(int time)
        {
            SortKeyframes();
            Type0_Keyframe prev = null;

            foreach(var keyframe in Keyframes)
            {
                if (keyframe.Index >= time) break;
                prev = keyframe;
            }

            return prev;
        }

        /// <summary>
        /// Returns the keyframe that appears just after the specified frame
        /// </summary>
        /// <returns></returns>
        public Type0_Keyframe GetKeyframeAfter(int time)
        {
            SortKeyframes();
            foreach (var keyframe in Keyframes)
            {
                if (keyframe.Index > time) return keyframe;
            }

            return null;
        }

        public void SortKeyframes()
        {
            Sorting.SortEntries2(Keyframes);
        }
    
    }

    [Serializable]
    [YAXSerializeAs("Keyframe")]
    public class Type0_Keyframe : IKeyframe, ISortable
    {
        [YAXDontSerialize]
        public int SortID { get { return Index; } }

        [YAXAttributeForClass]
        public short Index { get; set; }
        [YAXAttributeForClass]
        [YAXFormat("0.0######")]
        public float Float { get; set; }
    }

    //Type 1 data

    [Serializable]
    [YAXSerializeAs("Animation_SubGroup")]
    public class Type1_Header
    {
        [YAXDontSerialize]
        public string Name
        {
            get
            {
                return "Entry";
            }
        }

        [YAXAttributeForClass]
        public byte I_00 { get; set; }
        [YAXAttributeForClass]
        public byte I_01 { get; set; }
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "Animation")]
        public AsyncObservableCollection<Type0> Entries { get; set; }

        public Type1_Header Clone()
        {
            AsyncObservableCollection<Type0> _Entries = AsyncObservableCollection<Type0>.Create();
            foreach (var e in Entries)
            {
                _Entries.Add(e.Clone());
            }

            return new Type1_Header()
            {
                I_00 = I_00,
                I_01 = I_01,
                Entries = _Entries
            };
        }

        public static Type1_Header GetNew()
        {
            return new Type1_Header()
            {
                Entries = AsyncObservableCollection<Type0>.Create()
            };
        }
    }


    //Section 2: EMB
    [Serializable]
    [YAXSerializeAs("Texture")]
    public class EMP_TextureDefinition : INotifyPropertyChanged
    {
        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        public void NotifyPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private EMB_CLASS.EmbEntry _textureRef = null;
        [YAXDontSerialize]
        public EMB_CLASS.EmbEntry TextureRef
        {
            get
            {
                return this._textureRef;
            }

            set
            {
                if (value != this._textureRef)
                {
                    this._textureRef = value;
                    NotifyPropertyChanged("TextureRef");
                    NotifyPropertyChanged("ToolName");
                }
            }
        }

        [YAXDontSerialize]
        public string ToolName
        {
            get
            {
                if(TextureRef == null) return String.Format("Texture {0} (No Assigned Texture)", EntryIndex);
                return String.Format("Texture {0} ({1})", EntryIndex, TextureRef.Name);
            }
        }

        private TextureAnimationType TextureTypeValue = TextureAnimationType.Static;
        [YAXDontSerialize]
        public TextureAnimationType TextureType
        {
            get
            {
                return this.TextureTypeValue;
            }

            set
            {
                if (value != this.TextureTypeValue)
                {
                    this.TextureTypeValue = value;
                    NotifyPropertyChanged("TextureType");
                    NotifyPropertyChanged("IsType0Visible");
                    NotifyPropertyChanged("IsType1Visible");
                    NotifyPropertyChanged("IsType2Visible");
                }
            }
        }

        private int _entryIndex = 0;
        [YAXAttributeForClass]
        [YAXSerializeAs("ID")]
        public int EntryIndex
        {
            get
            {
                return this._entryIndex;
            }

            set
            {
                if (value != this._entryIndex)
                {
                    this._entryIndex = value;
                    NotifyPropertyChanged("EntryIndex");
                    NotifyPropertyChanged("ToolName");
                }
            }
        }
        
        [YAXAttributeForClass]
        [YAXSerializeAs("TextureIndex")]
        public byte I_01 { get; set; } = byte.MaxValue;

        [YAXAttributeFor("I_00")]
        [YAXSerializeAs("value")]
        public byte I_00 { get; set; }
        [YAXAttributeFor("I_02")]
        [YAXSerializeAs("value")]
        public byte I_02 { get; set; }
        [YAXAttributeFor("I_03")]
        [YAXSerializeAs("value")]
        public byte I_03 { get; set; }
        [YAXAttributeFor("Filtering")]
        [YAXSerializeAs("Min")]
        public byte I_04 { get; set; } //int8
        [YAXAttributeFor("Filtering")]
        [YAXSerializeAs("Max")]
        public byte I_05 { get; set; } //int8
        [YAXAttributeFor("TextureRepetition")]
        [YAXSerializeAs("X")]
        public TextureRepitition I_06_byte { get; set; } //int8
        [YAXAttributeFor("TextureRepetition")]
        [YAXSerializeAs("Y")]
        public TextureRepitition I_07_byte { get; set; } //int8
        [YAXAttributeFor("EnableRandomSymetry")]
        [YAXSerializeAs("X")]
        public byte I_08 { get; set; } //int8
        [YAXAttributeFor("EnableRandomSymetry")]
        [YAXSerializeAs("Y")]
        public byte I_09 { get; set; } //int8

        private ScrollAnimation _subData2 = null;
        [YAXDontSerializeIfNull]
        [YAXSerializeAs("ScrollAnimationType")]
        public ScrollAnimation SubData2
        {
            get
            {
                return this._subData2;
            }

            set
            {
                if (value != this._subData2)
                {
                    this._subData2 = value;
                    NotifyPropertyChanged("SubData2");
                }
            }
        }


        public void ReplaceValues(EMP_TextureDefinition newValues, List<IUndoRedo> undos = null)
        {
            if (undos == null) undos = new List<IUndoRedo>();

            //Utils.CopyValues only copies primitives - this must be copied manually.
            undos.Add(new UndoableProperty<EMP_TextureDefinition>(nameof(TextureRef), this, TextureRef, newValues.TextureRef));
            TextureRef = newValues.TextureRef;

            if(newValues.SubData2 != null && SubData2 != null)
            {
                undos.Add(new UndoableProperty<ScrollAnimation>(nameof(SubData2.ScrollSpeed_U), SubData2, SubData2.ScrollSpeed_U, newValues.SubData2.ScrollSpeed_U));
                undos.Add(new UndoableProperty<ScrollAnimation>(nameof(SubData2.ScrollSpeed_V), SubData2, SubData2.ScrollSpeed_V, newValues.SubData2.ScrollSpeed_V));
                undos.Add(new UndoableProperty<ScrollAnimation>(nameof(SubData2.Keyframes), SubData2, SubData2.Keyframes, newValues.SubData2.Keyframes));
                undos.Add(new UndoableProperty<ScrollAnimation>(nameof(SubData2.useSpeedInsteadOfKeyFrames), SubData2, SubData2.useSpeedInsteadOfKeyFrames, newValues.SubData2.useSpeedInsteadOfKeyFrames));

                SubData2.ScrollSpeed_U = newValues.SubData2.ScrollSpeed_U;
                SubData2.ScrollSpeed_V = newValues.SubData2.ScrollSpeed_V;
                SubData2.Keyframes = newValues.SubData2.Keyframes;
                SubData2.useSpeedInsteadOfKeyFrames = newValues.SubData2.useSpeedInsteadOfKeyFrames;
            }

            //Copy remaining values
            undos.AddRange(Utils.CopyValues(this, newValues, nameof(EntryIndex)));

            undos.Add(new UndoActionPropNotify(this, true));
            this.NotifyPropsChanged();
        }
        
        [YAXDontSerialize]
        public IEnumerable<TextureAnimationType> TextureAnimationTypes
        {
            get
            {
                return Enum.GetValues(typeof(TextureAnimationType))
                    .Cast<TextureAnimationType>();
            }
        }

        [YAXDontSerialize]
        public IEnumerable<TextureRepitition> TextureRepititions
        {
            get
            {
                return Enum.GetValues(typeof(TextureRepitition))
                    .Cast<TextureRepitition>();
            }
        }

        [YAXDontSerialize]
        public Visibility IsType0Visible
        {
            get { return (TextureType == TextureAnimationType.Static) ? Visibility.Visible : Visibility.Hidden; }
        }

        [YAXDontSerialize]
        public Visibility IsType1Visible
        {
            get { return (TextureType == TextureAnimationType.Speed) ? Visibility.Visible : Visibility.Hidden; }
        }

        [YAXDontSerialize]
        public Visibility IsType2Visible
        {
            get { return (TextureType == TextureAnimationType.SpriteSheet) ? Visibility.Visible : Visibility.Hidden; }
        }


        public enum TextureAnimationType
        {
            Static = 0,
            Speed = 1,
            SpriteSheet = 2
        }

        public enum TextureRepitition
        {
            Wrap = 0,
            Mirror = 1,
            Clamp = 2,
            Border = 3
        }

        public TextureAnimationType CalculateTextureType()
        {
            if (SubData2.useSpeedInsteadOfKeyFrames == true)
            {
                return EMP_TextureDefinition.TextureAnimationType.Speed;
            }
            if (SubData2.Keyframes.Count() == 1 && SubData2.Keyframes[0].I_00 == -1)
            {
                return EMP_TextureDefinition.TextureAnimationType.Static;
            }
            else
            {
                return EMP_TextureDefinition.TextureAnimationType.SpriteSheet;
            }
        }
        

        public EMP_TextureDefinition Clone()
        {
            return new EMP_TextureDefinition()
            {
                I_00 = I_00,
                I_01 = I_01,
                I_02 = I_02,
                I_03 = I_03,
                I_04 = I_04,
                I_05 = I_05,
                I_06_byte = I_06_byte,
                I_07_byte = I_07_byte,
                I_08 = I_08,
                I_09 = I_09,
                EntryIndex = EntryIndex,
                SubData2 = SubData2.Clone(),
                TextureTypeValue = TextureTypeValue,
                TextureRef = TextureRef
            };
        }

        public static EMP_TextureDefinition GetNew()
        {
            var keyframes = AsyncObservableCollection<SubData_2_Entry>.Create();
            keyframes.Add(new SubData_2_Entry());

            return new EMP_TextureDefinition()
            {
                EntryIndex = byte.MaxValue,
                TextureType = TextureAnimationType.Static,
                I_06_byte = TextureRepitition.Wrap,
                I_07_byte = TextureRepitition.Wrap,
                SubData2 = new ScrollAnimation()
                {
                    Keyframes = keyframes,
                    useSpeedInsteadOfKeyFrames = false
                }
            };
        }
        
        public bool Compare(EMP_TextureDefinition obj2)
        {
            return Compare(this, obj2);
        }

        public static bool Compare(EMP_TextureDefinition obj1, EMP_TextureDefinition obj2)
        {
            if (obj1.I_00 != obj2.I_00) return false;
            if (obj1.I_01 != obj2.I_01) return false;
            if (obj1.I_02 != obj2.I_02) return false;
            if (obj1.I_03 != obj2.I_03) return false;
            if (obj1.I_04 != obj2.I_04) return false;
            if (obj1.I_05 != obj2.I_05) return false;
            if (obj1.I_08 != obj2.I_08) return false;
            if (obj1.I_06_byte != obj2.I_06_byte) return false;
            if (obj1.I_07_byte != obj2.I_07_byte) return false;
            if (obj1.I_09 != obj2.I_09) return false;
            if (obj1.TextureType != obj2.TextureType) return false;

            if(obj1.SubData2 != null)
            {
                if (!obj1.SubData2.Compare(obj2.SubData2)) return false;
            }

            if(obj1.TextureRef != null && obj2.TextureRef != null)
            {
                if (!obj1.TextureRef.Compare(obj2.TextureRef, true)) return false;
            }
            else if (obj1.TextureRef == null && obj2.TextureRef == null)
            {

            }
            else
            {
                return false;
            }

            return true;
        }
    
        /*
        public static bool IsRepeatingTexture(EmbEntry embEntry, EffectContainer.AssetContainerTool assetContainer)
        {
            foreach(var emp in assetContainer.Assets)
            {
                if(emp.Files?.Count > 0)
                {
                    foreach (var textureDef in emp.Files[0].EmpFile.Textures)
                    {
                        if(textureDef.TextureRef == embEntry)
                        {
                            if(textureDef.SubData2 != null)
                            {
                                if (!textureDef.SubData2.useSpeedInsteadOfKeyFrames)
                                {
                                    foreach(var keyframe in textureDef.SubData2.Keyframes)
                                    {
                                        if (keyframe.ScaleX > 1f || keyframe.ScaleY > 1f) return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }
        */
    }

    [Serializable]
    [YAXSerializeAs("AnimationScrollType")]
    public class ScrollAnimation : INotifyPropertyChanged
    {

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
        
        private void NotifyPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        [YAXAttributeForClass]
        [YAXSerializeAs("UseSpeedInsteadKeyFrames")]
        public bool useSpeedInsteadOfKeyFrames { get; set; }
        [YAXAttributeFor("ScrollSpeed")]
        [YAXSerializeAs("U")]
        [YAXFormat("0.0###########")]
        public float ScrollSpeed_U { get; set; }
        [YAXAttributeFor("ScrollSpeed")]
        [YAXSerializeAs("V")]
        [YAXFormat("0.0###########")]
        public float ScrollSpeed_V { get; set; }

        private AsyncObservableCollection<SubData_2_Entry> _keyframes = null;
        [YAXCollection(YAXCollectionSerializationTypes.Recursive, EachElementName = "Keyframe")]
        public AsyncObservableCollection<SubData_2_Entry> Keyframes
        {
            get
            {
                return this._keyframes;
            }

            set
            {
                if (value != this._keyframes)
                {
                    this._keyframes = value;
                    NotifyPropertyChanged(nameof(Keyframes));
                }
            }
        }

        public ScrollAnimation Clone()
        {
            AsyncObservableCollection<SubData_2_Entry> _Keyframes = AsyncObservableCollection<SubData_2_Entry>.Create();

            if(Keyframes != null)
            {
                foreach (var e in Keyframes)
                {
                    _Keyframes.Add(e.Clone());
                }
            }

            return new ScrollAnimation()
            {
                useSpeedInsteadOfKeyFrames = useSpeedInsteadOfKeyFrames,
                ScrollSpeed_U = ScrollSpeed_U,
                ScrollSpeed_V = ScrollSpeed_V,
                Keyframes = _Keyframes
            };
        }

        public bool Compare(ScrollAnimation obj2)
        {
            return Compare(this, obj2);
        }

        public static bool Compare(ScrollAnimation obj1, ScrollAnimation obj2)
        {
            if (obj2 == null && obj1 == null) return true;
            if (obj1 != null && obj2 == null) return false;
            if (obj2 != null && obj1 == null) return false;

            if (obj1.useSpeedInsteadOfKeyFrames != obj2.useSpeedInsteadOfKeyFrames) return false;
            if (obj1.ScrollSpeed_U != obj2.ScrollSpeed_U) return false;
            if (obj1.ScrollSpeed_V != obj2.ScrollSpeed_V) return false;

            if(obj1.Keyframes != null && obj2.Keyframes != null)
            {
                if (obj1.Keyframes.Count != obj2.Keyframes.Count) return false;

                for(int i = 0; i < obj1.Keyframes.Count; i++)
                {
                    if (!obj1.Keyframes[i].Compare(obj2.Keyframes[i])) return false;
                }
            }
            else if(obj1.Keyframes == null && obj2.Keyframes == null)
            {
                //Both are null. OK
            }
            else
            {
                //Mismatch
                return false;
            }

            return true;
        }
    }

    [Serializable]
    [YAXSerializeAs("Keyframe")]
    public class SubData_2_Entry
    {
        [YAXAttributeForClass]
        [YAXSerializeAs("Time")]
        public int I_00 { get; set; }
        [YAXAttributeForClass]
        [YAXSerializeAs("Scroll_X")]
        [YAXFormat("0.0###########")]
        public float ScrollX { get; set; }
        [YAXAttributeForClass]
        [YAXSerializeAs("Scroll_Y")]
        [YAXFormat("0.0###########")]
        public float ScrollY { get; set; }
        [YAXAttributeForClass]
        [YAXSerializeAs("Scale_X")]
        [YAXFormat("0.0###########")]
        public float ScaleX { get; set; }
        [YAXAttributeForClass]
        [YAXSerializeAs("Scale_Y")]
        [YAXFormat("0.0###########")]
        public float ScaleY { get; set; }
        [YAXAttributeForClass]
        [YAXDontSerializeIfNull]
        [YAXSerializeAs("F_20")]
        public string F_20 { get; set; } //float
        [YAXAttributeForClass]
        [YAXDontSerializeIfNull]
        [YAXSerializeAs("F_24")]
        public string F_24 { get; set; } //float

        public SubData_2_Entry Clone()
        {
            return new SubData_2_Entry()
            {
                I_00 = I_00,
                ScaleX = ScaleX,
                ScrollY = ScrollY,
                ScrollX = ScrollX,
                ScaleY = ScaleY,
                F_20 = "0.0",
                F_24 = "0.0"
            };
        }

        public void SetDefaultValuesForSDBH()
        {
            if (F_20 == null)
                F_20 = "0.0";
            if (F_24 == null)
                F_24 = "0.0";
        }

        public bool Compare(SubData_2_Entry obj2)
        {
            return Compare(this, obj2);
        }

        public static bool Compare(SubData_2_Entry obj1, SubData_2_Entry obj2)
        {
            if (obj1.I_00 != obj2.I_00) return false;
            if (obj1.ScrollX != obj2.ScrollX) return false;
            if (obj1.ScrollY != obj2.ScrollY) return false;
            if (obj1.ScaleX != obj2.ScaleX) return false;
            if (obj1.ScaleY != obj2.ScaleY) return false;
            if (obj1.F_20 != obj2.F_20) return false;
            if (obj1.F_24 != obj2.F_24) return false;

            return true;
        }
    }

    [Serializable]
    public class TextureEntry_Ref : INotifyPropertyChanged
    {
        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
        
        private void NotifyPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private EMP_TextureDefinition _textureRef = null;
        public EMP_TextureDefinition TextureRef
        {
            get
            {
                return this._textureRef;
            }

            set
            {
                if (value != this._textureRef)
                {
                    this._textureRef = value;
                    NotifyPropertyChanged(nameof(TextureRef));
                    NotifyPropertyChanged(nameof(UndoableTextureRef));
                }
            }
        }
    
        public EMP_TextureDefinition UndoableTextureRef
        {
            get
            {
                return TextureRef;
            }
            set
            {
                if(TextureRef != value)
                {
                    UndoManager.Instance.AddUndo(new UndoableProperty<TextureEntry_Ref>(nameof(TextureRef), this, TextureRef, value, "Texture Ref"));
                    TextureRef = value;
                    NotifyPropertyChanged(nameof(TextureRef));
                    NotifyPropertyChanged(nameof(UndoableTextureRef));
                }
            }
        }
    }

    //Interface
    public interface IKeyframe
    {
        short Index { get; set; }
        float Float { get; set; }
    }

    public interface IFloatSize4
    {
        float F_16 { get; set; }
        float F_20 { get; set; }
        float F_24 { get; set; }
        float F_28 { get; set; }
    }

    public interface IFloatSize8
    {
        float F_00 { get; set; }
        float F_04 { get; set; }
        float F_08 { get; set; }
        float F_12 { get; set; }
        float F_16 { get; set; }
        float F_20 { get; set; }
        float F_24 { get; set; }
        float F_28 { get; set; }
    }

    public interface IFloatSize12Type0
    {
        float F_00 { get; set; }
        float F_04 { get; set; }
        float F_08 { get; set; }
        float F_12 { get; set; }
        float F_16 { get; set; }
        float F_20 { get; set; }
        float F_24 { get; set; }
        float F_28 { get; set; }
        float F_32 { get; set; }
        float F_36 { get; set; }
        float F_40 { get; set; }
        float F_44 { get; set; }
    }

    public interface IFloatSize12Type1
    {
        float F_00 { get; set; }
        float F_04 { get; set; }
        float F_08 { get; set; }
        float F_12 { get; set; }
        float F_16 { get; set; }
        float F_20 { get; set; }
        float F_24 { get; set; }
        float F_28 { get; set; }
        float F_32 { get; set; }
        float F_36 { get; set; }
        string I_40 { get; set; }
        float F_44 { get; set; }

        int GetShape();

    }



}

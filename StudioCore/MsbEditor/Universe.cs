﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using SoulsFormats;

namespace StudioCore.MsbEditor
{
    /// <summary>
    /// A universe is a collection of loaded maps with methods to load, serialize,
    /// and unload individual maps.
    /// </summary>
    public class Universe
    {
        public List<Map> LoadedMaps { get; private set; } = new List<Map>();
        private AssetLocator AssetLocator;
        private Resource.ResourceManager ResourceMan;
        private Scene.RenderScene RenderScene;

        public Universe(AssetLocator al, Resource.ResourceManager rm,
            Scene.RenderScene scene)
        {
            AssetLocator = al;
            ResourceMan = rm;
            RenderScene = scene;
        }

        public Map GetLoadedMap(string id)
        {
            if (id != null)
            {
                foreach (var m in LoadedMaps)
                {
                    if (m.MapId == id)
                    {
                        return m;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Creates a drawable for a model and registers it with the scene. Will load
        /// the required assets in the background if they aren't already loaded.
        /// </summary>
        /// <param name="modelname"></param>
        /// <returns></returns>
        public Scene.IDrawable GetModelDrawable(Map map, MapObject obj, string modelname)
        {
            AssetDescription asset;
            bool loadcol = false;
            bool loadnav = false;
            Scene.RenderFilter filt = Scene.RenderFilter.All;
            var job = ResourceMan.CreateNewJob($@"Loading mesh");
            if (modelname.StartsWith("m"))
            {
                asset = AssetLocator.GetMapModel(map.MapId, AssetLocator.MapModelNameToAssetName(map.MapId, modelname));
                filt = Scene.RenderFilter.MapPiece;
            }
            else if (modelname.StartsWith("c"))
            {
                asset = AssetLocator.GetChrModel(modelname);
                filt = Scene.RenderFilter.Character;
            }
            else if (modelname.StartsWith("o"))
            {
                asset = AssetLocator.GetObjModel(modelname);
                filt = Scene.RenderFilter.Object;
            }
            else if (modelname.StartsWith("h"))
            {
                loadcol = true;
                asset = AssetLocator.GetMapCollisionModel(map.MapId, AssetLocator.MapModelNameToAssetName(map.MapId, modelname));
                filt = Scene.RenderFilter.Collision;
            }
            else if (modelname.StartsWith("n"))
            {
                loadnav = true;
                asset = AssetLocator.GetMapNVMModel(map.MapId, AssetLocator.MapModelNameToAssetName(map.MapId, modelname));
                filt = Scene.RenderFilter.Navmesh;
            }
            else
            {
                asset = AssetLocator.GetNullAsset();
            }

            if (loadcol)
            {
                var res = ResourceMan.GetResource<Resource.HavokCollisionResource>(asset.AssetVirtualPath);
                var mesh = new Scene.CollisionMesh(RenderScene, res, AssetLocator.Type == GameType.DarkSoulsIISOTFS);
                mesh.WorldMatrix = obj.GetTransform().WorldMatrix;
                obj.RenderSceneMesh = mesh;
                mesh.Selectable = new WeakReference<Scene.ISelectable>(obj);
                if (!res.IsLoaded)
                {
                    if (asset.AssetArchiveVirtualPath != null)
                    {
                        job.AddLoadArchiveTask(asset.AssetArchiveVirtualPath, false);
                    }
                    else if (asset.AssetVirtualPath != null)
                    {
                        job.AddLoadFileTask(asset.AssetVirtualPath);
                    }
                    job.StartJobAsync();
                }
                return mesh;
            }
            else if (loadnav && AssetLocator.Type != GameType.DarkSoulsIISOTFS)
            {
                var res = ResourceMan.GetResource<Resource.NVMNavmeshResource>(asset.AssetVirtualPath);
                var mesh = new Scene.NvmMesh(RenderScene, res, false);
                mesh.WorldMatrix = obj.GetTransform().WorldMatrix;
                obj.RenderSceneMesh = mesh;
                mesh.Selectable = new WeakReference<Scene.ISelectable>(obj);
                if (!res.IsLoaded)
                {
                    if (asset.AssetArchiveVirtualPath != null)
                    {
                        job.AddLoadArchiveTask(asset.AssetArchiveVirtualPath, false);
                    }
                    else if (asset.AssetVirtualPath != null)
                    {
                        job.AddLoadFileTask(asset.AssetVirtualPath);
                    }
                    job.StartJobAsync();
                }
                return mesh;
            }
            else if (loadnav && AssetLocator.Type == GameType.DarkSoulsIISOTFS)
            {

            }
            else
            {
                var res = ResourceMan.GetResource<Resource.FlverResource>(asset.AssetVirtualPath);
                var model = new NewMesh(RenderScene, res, false);
                model.DrawFilter = filt;
                model.WorldMatrix = obj.GetTransform().WorldMatrix;
                obj.RenderSceneMesh = model;
                model.Selectable = new WeakReference<Scene.ISelectable>(obj);
                if (!res.IsLoaded)
                {
                    if (asset.AssetArchiveVirtualPath != null)
                    {
                        job.AddLoadArchiveTask(asset.AssetArchiveVirtualPath, false, Resource.ResourceManager.ResourceType.Flver);
                    }
                    else if (asset.AssetVirtualPath != null)
                    {
                        job.AddLoadFileTask(asset.AssetVirtualPath);
                    }
                    job.StartJobAsync();
                }
                return model;
            }
            return null;
        }

        public void LoadDS2Generators(string mapid, Map map)
        {
            Dictionary<long, PARAM.Row> registParams = new Dictionary<long, PARAM.Row>();
            Dictionary<long, MergedParamRow> generatorParams = new Dictionary<long, MergedParamRow>();
            Dictionary<long, MapObject> generatorObjs = new Dictionary<long, MapObject>();

            var regparamad = AssetLocator.GetDS2GeneratorRegistParam(mapid);
            var regparam = PARAM.Read(regparamad.AssetPath);
            var reglayout = PARAM.Layout.ReadXMLFile($@"Assets\ParamLayouts\DS2SOTFS\{regparam.ID}.xml");
            regparam.SetLayout(reglayout);
            foreach (var row in regparam.Rows)
            {
                if (row.Name == null || row.Name == "")
                {
                    row.Name = "regist_" + row.ID.ToString();
                }
                registParams.Add(row.ID, row);

                var obj = new MapObject(map, row, MapObject.ObjectType.TypeDS2GeneratorRegist);
                map.AddObject(obj);
            }

            var locparamad = AssetLocator.GetDS2GeneratorLocationParam(mapid);
            var locparam = PARAM.Read(locparamad.AssetPath);
            var loclayout = PARAM.Layout.ReadXMLFile($@"Assets\ParamLayouts\DS2SOTFS\{locparam.ID}.xml");
            locparam.SetLayout(loclayout);
            foreach (var row in locparam.Rows)
            {
                if (row.Name == null || row.Name == "")
                {
                    row.Name = "generator_" + row.ID.ToString();
                }

                // Offset the generators by the map offset
                row["PositionX"].Value = (float)row["PositionX"].Value + map.MapOffset.Position.X;
                row["PositionY"].Value = (float)row["PositionY"].Value + map.MapOffset.Position.Y;
                row["PositionZ"].Value = (float)row["PositionZ"].Value + map.MapOffset.Position.Z;

                var mergedRow = new MergedParamRow();
                mergedRow.AddRow("generator-loc", row);
                generatorParams.Add(row.ID, mergedRow);

                var obj = new MapObject(map, mergedRow, MapObject.ObjectType.TypeDS2Generator);
                generatorObjs.Add(row.ID, obj);
                map.AddObject(obj);
            }

            var chrsToLoad = new HashSet<AssetDescription>();
            var genparamad = AssetLocator.GetDS2GeneratorParam(mapid);
            var genparam = PARAM.Read(genparamad.AssetPath);
            var genlayout = PARAM.Layout.ReadXMLFile($@"Assets\ParamLayouts\DS2SOTFS\{genparam.ID}.xml");
            genparam.SetLayout(genlayout);
            foreach (var row in genparam.Rows)
            {
                if (row.Name == null || row.Name == "")
                {
                    row.Name = "generator_" + row.ID.ToString();
                }

                if (generatorParams.ContainsKey(row.ID))
                {
                    generatorParams[row.ID].AddRow("generator", row);
                }
                else
                {
                    var mergedRow = new MergedParamRow();
                    mergedRow.AddRow("generator", row);
                    generatorParams.Add(row.ID, mergedRow);
                    var obj = new MapObject(map, mergedRow, MapObject.ObjectType.TypeDS2Generator);
                    generatorObjs.Add(row.ID, obj);
                    map.AddObject(obj);
                }

                uint registid = (uint)row["GeneratorRegistParam"].Value;
                if (registParams.ContainsKey(registid))
                {
                    var regist = registParams[registid];
                    var chrid = ParamBank.GetChrIDForEnemy((uint)regist["EnemyParamID"].Value);
                    if (chrid != null)
                    {
                        var asset = AssetLocator.GetChrModel($@"c{chrid}");
                        var res = ResourceMan.GetResource<Resource.FlverResource>(asset.AssetVirtualPath);
                        var model = new NewMesh(RenderScene, res, false);
                        model.DrawFilter = Scene.RenderFilter.Character;
                        generatorObjs[row.ID].RenderSceneMesh = model;
                        model.Selectable = new WeakReference<Scene.ISelectable>(generatorObjs[row.ID]);
                        chrsToLoad.Add(asset);
                    }
                }
            }

            var job = ResourceMan.CreateNewJob($@"Loading chrs");
            foreach (var chr in chrsToLoad)
            {
                if (chr.AssetArchiveVirtualPath != null)
                {
                    job.AddLoadArchiveTask(chr.AssetArchiveVirtualPath, false, Resource.ResourceManager.ResourceType.Flver);
                }
                else if (chr.AssetVirtualPath != null)
                {
                    job.AddLoadFileTask(chr.AssetVirtualPath);
                }
            }
            job.StartJobAsync();
        }

        public void LoadMap(string mapid)
        {
            var map = new Map(this, mapid);

            var chrsToLoad = new HashSet<AssetDescription>();
            var objsToLoad = new HashSet<AssetDescription>();
            var colsToLoad = new HashSet<AssetDescription>();
            var navsToLoad = new HashSet<AssetDescription>();

            var ad = AssetLocator.GetMapMSB(mapid);
            IMsb msb;
            if (AssetLocator.Type == GameType.DarkSoulsIII)
            {
                msb = MSB3.Read(ad.AssetPath);
            }
            else if (AssetLocator.Type == GameType.Sekiro)
            {
                msb = MSBS.Read(ad.AssetPath);
            }
            else if (AssetLocator.Type == GameType.DarkSoulsIISOTFS)
            {
                msb = MSB2.Read(ad.AssetPath);
            }
            else
            {
                msb = MSB1.Read(ad.AssetPath);
            }
            map.LoadMSB(msb);

            // Temporary garbage
            foreach (var obj in map.MapObjects)
            {
                if (obj.MsbObject is IMsbPart mp && mp.ModelName != null && mp.ModelName != "")
                {
                    AssetDescription asset;
                    bool loadcol = false;
                    bool loadnav = false;
                    bool usedrawgroups = false;
                    Scene.RenderFilter filt = Scene.RenderFilter.All;
                    if (mp.ModelName.StartsWith("m"))
                    {
                        asset = AssetLocator.GetMapModel(mapid, AssetLocator.MapModelNameToAssetName(mapid, mp.ModelName));
                        filt = Scene.RenderFilter.MapPiece;
                        obj.UseDrawGroups = true;
                    }
                    else if (mp.ModelName.StartsWith("c"))
                    {
                        asset = AssetLocator.GetChrModel(mp.ModelName);
                        filt = Scene.RenderFilter.Character;
                        chrsToLoad.Add(asset);
                    }
                    else if (mp.ModelName.StartsWith("o"))
                    {
                        asset = AssetLocator.GetObjModel(mp.ModelName);
                        filt = Scene.RenderFilter.Object;
                        objsToLoad.Add(asset);
                    }
                    else if (mp.ModelName.StartsWith("h"))
                    {
                        loadcol = true;
                        asset = AssetLocator.GetMapCollisionModel(mapid, AssetLocator.MapModelNameToAssetName(mapid, mp.ModelName));
                        filt = Scene.RenderFilter.Collision;
                        colsToLoad.Add(asset);
                    }
                    else if (mp.ModelName.StartsWith("n"))
                    {
                        loadnav = true;
                        asset = AssetLocator.GetMapNVMModel(mapid, AssetLocator.MapModelNameToAssetName(mapid, mp.ModelName));
                        filt = Scene.RenderFilter.Navmesh;
                        navsToLoad.Add(asset);
                    }
                    else
                    {
                        asset = AssetLocator.GetNullAsset();
                    }

                    if (loadcol)
                    {
                        var res = ResourceMan.GetResource<Resource.HavokCollisionResource>(asset.AssetVirtualPath);
                        var mesh = new Scene.CollisionMesh(RenderScene, res, AssetLocator.Type == GameType.DarkSoulsIISOTFS);
                        mesh.WorldMatrix = obj.GetTransform().WorldMatrix;
                        obj.RenderSceneMesh = mesh;
                        mesh.Selectable = new WeakReference<Scene.ISelectable>(obj);
                    }
                    else if (loadnav && AssetLocator.Type != GameType.DarkSoulsIISOTFS)
                    {
                        var res = ResourceMan.GetResource<Resource.NVMNavmeshResource>(asset.AssetVirtualPath);
                        var mesh = new Scene.NvmMesh(RenderScene, res, false);
                        mesh.WorldMatrix = obj.GetTransform().WorldMatrix;
                        obj.RenderSceneMesh = mesh;
                        mesh.Selectable = new WeakReference<Scene.ISelectable>(obj);
                    }
                    else if (loadnav && AssetLocator.Type == GameType.DarkSoulsIISOTFS)
                    {

                    }
                    else
                    {
                        var res = ResourceMan.GetResource<Resource.FlverResource>(asset.AssetVirtualPath);
                        var model = new NewMesh(RenderScene, res, false);
                        model.DrawFilter = filt;
                        model.WorldMatrix = obj.GetTransform().WorldMatrix;
                        obj.RenderSceneMesh = model;
                        model.Selectable = new WeakReference<Scene.ISelectable>(obj);
                    }
                }
                if (obj.MsbObject is IMsbRegion r && r.Shape is MSB.Shape.Box b)
                {
                    var mesh = Scene.Region.GetBoxRegion(RenderScene);
                    mesh.WorldMatrix = obj.GetTransform().WorldMatrix;
                    obj.RenderSceneMesh = mesh;
                    mesh.Selectable = new WeakReference<Scene.ISelectable>(obj);
                }
                else if (obj.MsbObject is IMsbRegion r2 && r2.Shape is MSB.Shape.Sphere s)
                {
                    var mesh = Scene.Region.GetSphereRegion(RenderScene);
                    mesh.WorldMatrix = obj.GetTransform().WorldMatrix;
                    obj.RenderSceneMesh = mesh;
                    mesh.Selectable = new WeakReference<Scene.ISelectable>(obj);
                }
                else if (obj.MsbObject is IMsbRegion r3 && r3.Shape is MSB.Shape.Point p)
                {
                    var mesh = Scene.Region.GetPointRegion(RenderScene);
                    mesh.WorldMatrix = obj.GetTransform().WorldMatrix;
                    obj.RenderSceneMesh = mesh;
                    mesh.Selectable = new WeakReference<Scene.ISelectable>(obj);
                }

                // Try to find the map offset
                if (obj.MsbObject is MSB2.Event.MapOffset mo)
                {
                    var t = Transform.Default;
                    t.Position = mo.Translation;
                    map.MapOffset = t;
                }
            }
            LoadedMaps.Add(map);

            if (AssetLocator.Type == GameType.DarkSoulsIISOTFS)
            {
                LoadDS2Generators(mapid, map);
            }

            var job = ResourceMan.CreateNewJob($@"Loading {mapid} geometry");
            foreach (var mappiece in AssetLocator.GetMapModels(mapid))
            {
                if (mappiece.AssetArchiveVirtualPath != null)
                {
                    job.AddLoadArchiveTask(mappiece.AssetArchiveVirtualPath, false, Resource.ResourceManager.ResourceType.Flver);
                }
                else if (mappiece.AssetVirtualPath != null)
                {
                    job.AddLoadFileTask(mappiece.AssetVirtualPath);
                }
            }
            
            job.StartJobAsync();
            job = ResourceMan.CreateNewJob($@"Loading {mapid} collisions");
            foreach (var col in colsToLoad)
            {
                if (col.AssetArchiveVirtualPath != null)
                {
                    job.AddLoadArchiveTask(col.AssetArchiveVirtualPath, false);
                }
                else if (col.AssetVirtualPath != null)
                {
                    job.AddLoadFileTask(col.AssetVirtualPath);
                }
            }
            job.StartJobAsync();
            job = ResourceMan.CreateNewJob($@"Loading chrs");
            foreach (var chr in chrsToLoad)
            {
                if (chr.AssetArchiveVirtualPath != null)
                {
                    job.AddLoadArchiveTask(chr.AssetArchiveVirtualPath, false, Resource.ResourceManager.ResourceType.Flver);
                }
                else if (chr.AssetVirtualPath != null)
                {
                    job.AddLoadFileTask(chr.AssetVirtualPath);
                }
            }
            job.StartJobAsync();
            job = ResourceMan.CreateNewJob($@"Loading objs");
            foreach (var obj in objsToLoad)
            {
                if (obj.AssetArchiveVirtualPath != null)
                {
                    job.AddLoadArchiveTask(obj.AssetArchiveVirtualPath, false, Resource.ResourceManager.ResourceType.Flver);
                }
                else if (obj.AssetVirtualPath != null)
                {
                    job.AddLoadFileTask(obj.AssetVirtualPath);
                }
            }
            job.StartJobAsync();

            job = ResourceMan.CreateNewJob($@"Loading Navmeshes");
            foreach (var nav in navsToLoad)
            {
                if (nav.AssetArchiveVirtualPath != null)
                {
                    job.AddLoadArchiveTask(nav.AssetArchiveVirtualPath, false);
                }
                else if (nav.AssetVirtualPath != null)
                {
                    job.AddLoadFileTask(nav.AssetVirtualPath);
                }
            }
            job.StartJobAsync();
        }

        private void SaveDS2Generators(Map map)
        {
            // Load all the params
            var regparamad = AssetLocator.GetDS2GeneratorRegistParam(map.MapId);
            var regparamadw = AssetLocator.GetDS2GeneratorRegistParam(map.MapId, true);
            var regparam = PARAM.Read(regparamad.AssetPath);
            var reglayout = PARAM.Layout.ReadXMLFile($@"Assets\ParamLayouts\DS2SOTFS\{regparam.ID}.xml");
            regparam.SetLayout(reglayout);

            var locparamad = AssetLocator.GetDS2GeneratorLocationParam(map.MapId);
            var locparamadw = AssetLocator.GetDS2GeneratorLocationParam(map.MapId, true);
            var locparam = PARAM.Read(locparamad.AssetPath);
            var loclayout = PARAM.Layout.ReadXMLFile($@"Assets\ParamLayouts\DS2SOTFS\{locparam.ID}.xml");
            locparam.SetLayout(loclayout);

            var chrsToLoad = new HashSet<AssetDescription>();
            var genparamad = AssetLocator.GetDS2GeneratorParam(map.MapId);
            var genparamadw = AssetLocator.GetDS2GeneratorParam(map.MapId, true);
            var genparam = PARAM.Read(genparamad.AssetPath);
            var genlayout = PARAM.Layout.ReadXMLFile($@"Assets\ParamLayouts\DS2SOTFS\{genparam.ID}.xml");
            genparam.SetLayout(genlayout);

            // Clear them out
            regparam.Rows.Clear();
            locparam.Rows.Clear();
            genparam.Rows.Clear();

            // Serialize objects
            if (!map.SerializeDS2Generators(locparam, genparam))
            {
                return;
            }
            if (!map.SerializeDS2Regist(regparam))
            {
                return;
            }

            // Save all the params
            if (File.Exists(regparamadw.AssetPath + ".temp"))
            {
                File.Delete(regparamadw.AssetPath + ".temp");
            }
            regparam.Write(regparamadw.AssetPath + ".temp", SoulsFormats.DCX.Type.None);
            File.Copy(regparamadw.AssetPath, regparamadw.AssetPath + ".prev", true);
            File.Delete(regparamadw.AssetPath);
            File.Move(regparamadw.AssetPath + ".temp", regparamadw.AssetPath);

            if (File.Exists(locparamadw.AssetPath + ".temp"))
            {
                File.Delete(locparamadw.AssetPath + ".temp");
            }
            locparam.Write(locparamadw.AssetPath + ".temp", SoulsFormats.DCX.Type.None);
            File.Copy(locparamadw.AssetPath, locparamadw.AssetPath + ".prev", true);
            File.Delete(locparamadw.AssetPath);
            File.Move(locparamadw.AssetPath + ".temp", locparamadw.AssetPath);

            if (File.Exists(genparamadw.AssetPath + ".temp"))
            {
                File.Delete(genparamadw.AssetPath + ".temp");
            }
            genparam.Write(genparamadw.AssetPath + ".temp", SoulsFormats.DCX.Type.None);
            File.Copy(genparamadw.AssetPath, genparamadw.AssetPath + ".prev", true);
            File.Delete(genparamadw.AssetPath);
            File.Move(genparamadw.AssetPath + ".temp", genparamadw.AssetPath);
        }

        private void SaveMap(Map map)
        {
            var ad = AssetLocator.GetMapMSB(map.MapId);
            var adw = AssetLocator.GetMapMSB(map.MapId, true);
            IMsb msb;
            if (AssetLocator.Type == GameType.DarkSoulsIII)
            {
                msb = new MSB3();
            }
            else if (AssetLocator.Type == GameType.DarkSoulsIISOTFS)
            {
                MSB2 prev = MSB2.Read(ad.AssetPath);
                MSB2 n = new MSB2();
                n.PartPoses = prev.PartPoses;
                msb = n;
            }
            else if (AssetLocator.Type == GameType.Sekiro)
            {
                msb = new MSBS();
            }
            else
            {
                msb = new MSB1();
                //var t = MSB1.Read(ad.AssetPath);
                //((MSB1)msb).Models = t.Models;
            }

            map.SerializeToMSB(msb, AssetLocator.Type);

            // Write as a temporary file to make sure there are no errors before overwriting current file 
            string mapPath = adw.AssetPath;
            //if (GetModProjectPathForFile(mapPath) != null)
            //{
            //    mapPath = GetModProjectPathForFile(mapPath);
            //}

            if (File.Exists(mapPath + ".temp"))
            {
                File.Delete(mapPath + ".temp");
            }
            msb.Write(mapPath + ".temp", SoulsFormats.DCX.Type.None);

            // Make a copy of the previous map
            File.Copy(mapPath, mapPath + ".prev", true);

            // Move temp file as new map file
            File.Delete(mapPath);
            File.Move(mapPath + ".temp", mapPath);

            if (AssetLocator.Type == GameType.DarkSoulsIISOTFS)
            {
                SaveDS2Generators(map);
            }
        }

        public void SaveAllMaps()
        {
            foreach (var m in LoadedMaps)
            {
                SaveMap(m);
            }
        }
    }
}
// Pass 3D (fatia 1) — aplica malhas + câmeras extraídas offline
// (tools/dump_pass3d.py -> docs/pass3d_{file}.json) na cena construída.
// Sem isso, MeshFilter.sharedMesh é nulo e Camera usa defaults.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEngine;

namespace Unirecomp.Host
{
    public static class MeshLibrary
    {
        public static int AppliedMeshes, LinkedFilters, AppliedCameras;

        // Por MeshFilter: cores dos materiais do renderer ([r,g,b,a] por
        // submesh) + faixas de índices da malha ([firstElem,count] por
        // submesh). Ausente = branco + malha inteira.
        static readonly Dictionary<MeshFilter, (float[][] colors, int[][] ranges)> renderInfo = new();

        public static (float[][] colors, int[][] ranges) RenderInfo(MeshFilter mf) =>
            mf != null && renderInfo.TryGetValue(mf, out var v) ? v : (null, null);

        public static void Apply(BuiltScene scene, string file)
        {
            string path = $"docs/pass3d_{file}.json";
            if (!File.Exists(path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            renderInfo.Clear();

            var meshByPath = new Dictionary<long, Mesh>();
            foreach (var kv in root.GetProperty("meshes").EnumerateObject())
            {
                long pid = long.Parse(kv.Name);
                var m = kv.Value;
                int nv = 0, ni = 0;
                foreach (var _ in m.GetProperty("verts").EnumerateArray()) nv++;
                foreach (var _ in m.GetProperty("tris").EnumerateArray()) ni++;
                var verts = new Vector3[nv / 3];
                int i = 0;
                float x = 0, y = 0;
                int comp = 0;
                foreach (var e in m.GetProperty("verts").EnumerateArray())
                {
                    float v = (float)e.GetDouble();
                    if (comp == 0) x = v;
                    else if (comp == 1) y = v;
                    else verts[i++] = new Vector3(x, y, v);
                    comp = (comp + 1) % 3;
                }
                var tris = new int[ni];
                i = 0;
                foreach (var e in m.GetProperty("tris").EnumerateArray())
                    tris[i++] = e.GetInt32();
                var mesh = new Mesh { vertices = verts, triangles = tris };
                if (m.TryGetProperty("normals", out var nrm))
                {
                    int nn = 0;
                    foreach (var _ in nrm.EnumerateArray()) nn++;
                    if (nn == verts.Length * 3)
                    {
                        var normals = new Vector3[verts.Length];
                        i = 0;
                        float nx = 0, ny = 0;
                        int k = 0;
                        foreach (var e in nrm.EnumerateArray())
                        {
                            float v = (float)e.GetDouble();
                            if (k == 0) nx = v;
                            else if (k == 1) ny = v;
                            else normals[i++] = new Vector3(nx, ny, v);
                            k = (k + 1) % 3;
                        }
                        mesh.normals = normals;
                    }
                }
                meshByPath[pid] = mesh;
            }
            AppliedMeshes = meshByPath.Count;

            var rangesByMesh = new Dictionary<long, int[][]>();
            if (root.TryGetProperty("meshes", out var meshesEl))
                foreach (var kv in meshesEl.EnumerateObject())
                {
                    if (!long.TryParse(kv.Name, out long pid)) continue;
                    if (kv.Value.TryGetProperty("ranges", out var rg))
                    {
                        var list = new System.Collections.Generic.List<int[]>();
                        foreach (var e in rg.EnumerateArray())
                        {
                            int k = 0, first = 0, count = 0;
                            foreach (var n in e.EnumerateArray())
                            { if (k == 0) first = n.GetInt32(); else count = n.GetInt32(); k++; }
                            if (count > 0) list.Add(new[] { first, count });
                        }
                        if (list.Count > 0) rangesByMesh[pid] = list.ToArray();
                    }
                }
            var colorsByFilter = new Dictionary<long, float[][]>();
            if (root.TryGetProperty("rendererLinks", out var rl))
                foreach (var e in rl.EnumerateArray())
                {
                    if (!e.TryGetProperty("filter", out var fp)) continue;
                    var list = new System.Collections.Generic.List<float[]>();
                    if (e.TryGetProperty("colors", out var cs))
                        foreach (var c in cs.EnumerateArray())
                        {
                            var f = new float[4] { 1, 1, 1, 1 };
                            int k = 0;
                            foreach (var n in c.EnumerateArray())
                            { if (k < 4) f[k] = (float)n.GetDouble(); k++; }
                            list.Add(f);
                        }
                    colorsByFilter[fp.GetInt64()] = list.ToArray();
                }

            foreach (var e in root.GetProperty("meshLinks").EnumerateArray())
            {
                long filter = e.GetProperty("filter").GetInt64();
                long mesh = e.GetProperty("mesh").GetInt64();
                if (!scene.CompByPath.TryGetValue(filter, out var c)) continue;
                if (c is not MeshFilter mf) continue;
                if (meshByPath.TryGetValue(mesh, out var mm))
                {
                    mf.sharedMesh = mm;
                    LinkedFilters++;
                    colorsByFilter.TryGetValue(filter, out var cols);
                    rangesByMesh.TryGetValue(mesh, out var rng);
                    renderInfo[mf] = (cols, rng);
                }
            }

            foreach (var e in root.GetProperty("cameras").EnumerateArray())
            {
                long pathId = e.GetProperty("path").GetInt64();
                if (!scene.CompByPath.TryGetValue(pathId, out var c)) continue;
                if (c is not Camera cam) continue;
                cam.fieldOfView = (float)e.GetProperty("fov").GetDouble();
                cam.nearClipPlane = (float)e.GetProperty("near").GetDouble();
                cam.farClipPlane = (float)e.GetProperty("far").GetDouble();
                cam.depth = (float)e.GetProperty("depth").GetDouble();
                cam.orthographic = e.GetProperty("ortho").GetBoolean();
                var bg = e.GetProperty("bg");
                float r = 0, g = 0, b = 0;
                int k = 0;
                foreach (var v in bg.EnumerateArray())
                {
                    if (k == 0) r = (float)v.GetDouble();
                    else if (k == 1) g = (float)v.GetDouble();
                    else b = (float)v.GetDouble();
                    k++;
                }
                cam.backgroundColor = new Color(r, g, b, 1);
                AppliedCameras++;
            }
        }
    }
}

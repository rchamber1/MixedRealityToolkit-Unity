﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using System.Collections;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine.VR.WSA;

namespace HoloToolkit.Unity
{
    /// <summary>
    /// Handles the custom meshes generated by the understanding dll. The meshes
    /// are generated during the scanning phase and once more on scan finalization.
    /// The meshes can be used to visualize the scanning progress.
    /// </summary>
    public class SpatialUnderstandingCustomMesh : SpatialMappingSource
    {
        // Config
        [Tooltip("Indicate the time between mesh imports, during the scanning phase. A value of zero will disable pulling meshes from the dll")]
        public float ImportMeshPeriod = 1.0f;
        [Tooltip("Material used to render the custom mesh generated by the dll")]
        public Material MeshMaterial;

        // Properties
        /// <summary>
        /// Controls rendering of the mesh. This can be set by the user to hide or show the mesh.
        /// </summary>
        public bool DrawProcessedMesh
        {
            get
            {
                return drawProcessedMesh;
            }
            set
            {
                drawProcessedMesh = value;
                for (int i = 0; i < SurfaceObjects.Count; ++i)
                {
                    SurfaceObjects[i].Renderer.enabled = drawProcessedMesh;
                }
            }
        }

        protected override Material RenderMaterial { get { return MeshMaterial; } }
        
        /// <summary>
        /// World anchor used by the custom mesh
        /// </summary>
        public WorldAnchor LocalWorldAnchor { get; private set; }

        // Privates
        private bool drawProcessedMesh = true;
        private bool isImportActive = false;
        private DateTime timeLastImportedMesh = DateTime.Now;
        private Mesh customMesh;

        // Functions
        /// <summary>
        /// Imports the custom mesh from the dll. This a a coroutine which will take multiple frames to complete.
        /// </summary>
        /// <returns></returns>
        public IEnumerator Import_UnderstandingMesh()
        {
            if (!SpatialUnderstanding.Instance.AllowSpatialUnderstanding)
            {
                yield break;
            }

            SpatialUnderstandingDll dll = SpatialUnderstanding.Instance.UnderstandingDLL;
            Vector3[] meshVertices = null;
            Vector3[] meshNormals = null;
            Int32[] meshIndices = null;

            try
            {
                // Pull the mesh - first get the size, then allocate and pull the data
                int vertCount, idxCount;
                if ((SpatialUnderstandingDll.Imports.GeneratePlayspace_ExtractMesh_Setup(out vertCount, out idxCount) > 0) &&
                    (vertCount > 0) &&
                    (idxCount > 0))
                {
                    meshVertices = new Vector3[vertCount];
                    IntPtr vertPos = dll.PinObject(meshVertices);
                    meshNormals = new Vector3[vertCount];
                    IntPtr vertNorm = dll.PinObject(meshNormals);
                    meshIndices = new Int32[idxCount];
                    IntPtr indices = dll.PinObject(meshIndices);
                    SpatialUnderstandingDll.Imports.GeneratePlayspace_ExtractMesh_Extract(vertCount, vertPos, vertNorm, idxCount, indices);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.ToString());
            }

            // Wait a frame
            yield return null;

            try
            {
                // Clear
                Cleanup();

                // Create output mesh
                if ((meshVertices != null) &&
                    (meshVertices.Length > 0) &&
                    (meshIndices != null) &&
                    (meshIndices.Length > 0))
                {
                    if (customMesh != null)
                    {
                        Destroy(customMesh);
                    }
                    customMesh = new Mesh
                    {
                        vertices = meshVertices,
                        normals = meshNormals,
                        triangles = meshIndices
                    };
                    GameObject spatialMesh = AddSurfaceObject(customMesh, string.Format("SurfaceUnderstanding Mesh-{0}", transform.childCount), transform);
                    spatialMesh.AddComponent<UnityEngine.VR.WSA.WorldAnchor>();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.ToString());
            }

            // Wait a frame
            yield return null;

            // All done - can free up marshal pinned memory
            dll.UnpinAllObjects();

            // Done
            isImportActive = false;
        }

        /// <summary>
        /// Updates the mesh import process. This function will kick off the import 
        /// coroutine at the requested internal.
        /// </summary>
        /// <param name="deltaTime"></param>
        private void Update_MeshImport(float deltaTime)
        {
            // Only update every so often
            if ((ImportMeshPeriod <= 0.0f) ||
                ((DateTime.Now - timeLastImportedMesh).TotalSeconds < ImportMeshPeriod) ||
                (SpatialUnderstanding.Instance.ScanState != SpatialUnderstanding.ScanStates.Scanning))
            {
                return;
            }

            // Do an import
            if (!isImportActive)
            {
                StartCoroutine(Import_UnderstandingMesh());
                isImportActive = true;
            }

            // Mark it
            timeLastImportedMesh = DateTime.Now;
        }

        void OnDestroy()
        {
            Cleanup();
            if (customMesh != null)
            {
                Destroy(customMesh);
            }
        }

        void Update()
        {
            Update_MeshImport(Time.deltaTime);
        }
    }

}
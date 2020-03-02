﻿using Microsoft.MixedReality.Toolkit.SpatialAwareness;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Microsoft.MixedReality.Toolkit.Experimental.SpatialAwareness
{
    [MixedRealityDataProvider(
    typeof(IMixedRealitySpatialAwarenessSystem),
    SupportedPlatforms.WindowsUniversal,
    "Spatial Awareness Surface Plane Observer",
    "Experimental/Profiles/DefaultSurfacePlaneObserverProfile.asset",
    "MixedRealityToolkit.SDK")]
    public class SpatialAwarenessSurfacePlaneObserver : BaseSpatialObserver
    {
        [Tooltip("Currently active planes found within the Spatial Mapping Mesh.")]
        public List<GameObject> ActivePlanes;

        [Tooltip("Object used for creating and rendering Surface Planes.")]
        public GameObject SurfacePlanePrefab;

        [Tooltip("Minimum area required for a plane to be created.")]
        public float MinArea = 0.025f;

        /// <summary>
        /// Determines which plane types should be rendered.
        /// </summary>
        [HideInInspector]
        public SpatialAwarenessSurfaceTypes drawPlanesMask =
            (SpatialAwarenessSurfaceTypes.Wall | SpatialAwarenessSurfaceTypes.Floor | SpatialAwarenessSurfaceTypes.Ceiling | SpatialAwarenessSurfaceTypes.Platform);

        /// <summary>
        /// Determines which plane types should be discarded.
        /// Use this when the spatial mapping mesh is a better fit for the surface (ex: round tables).
        /// </summary>
        [HideInInspector]
        public SpatialAwarenessSurfaceTypes destroyPlanesMask = SpatialAwarenessSurfaceTypes.Unknown;

        /// <summary>
        /// Floor y value, which corresponds to the maximum horizontal area found below the user's head position.
        /// This value is reset by SurfaceMeshesToPlanes when the max floor plane has been found.
        /// </summary>
        public float FloorYPosition { get; private set; }

        /// <summary>
        /// Ceiling y value, which corresponds to the maximum horizontal area found above the user's head position.
        /// This value is reset by SurfaceMeshesToPlanes when the max ceiling plane has been found.
        /// </summary>
        public float CeilingYPosition { get; private set; }

        /// <summary>
        /// Delegate which is called when the MakePlanesCompleted event is triggered.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="args"></param>
        //public delegate void EventHandler(object source, EventArgs args);

        ///// <summary>
        ///// EventHandler which is triggered when the MakePlanesRoutine is finished.
        ///// </summary>
        //public event EventHandler MakePlanesComplete;

        /// <summary>
        /// Empty game object used to contain all planes created by the SurfaceToPlanes class.
        /// </summary>
        private GameObject planesParent;

        /// <summary>
        /// Used to align planes with gravity so that they appear more level.
        /// </summary>
        private float snapToGravityThreshold = 5.0f;

        /// <summary>
        /// Indicates if SurfaceToPlanes is currently creating planes based on the Spatial Mapping Mesh.
        /// </summary>
        private bool makingPlanes = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        /// <summary>
        /// How much time (in sec), while running in the Unity Editor, to allow RemoveSurfaceVertices to consume before returning control to the main program.
        /// </summary>
        //private static readonly float FrameTime = .016f;
#else
        /// <summary>
        /// How much time (in sec) to allow RemoveSurfaceVertices to consume before returning control to the main program.
        /// </summary>
        private static readonly float FrameTime = .008f;
#endif


        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">Friendly name of the service.</param>
        /// <param name="priority">Service priority. Used to determine order of instantiation.</param>
        /// <param name="profile">The service's configuration profile.</param>
        public SpatialAwarenessSurfacePlaneObserver(
            IMixedRealitySpatialAwarenessSystem spatialAwarenessSystem,
            string name = null,
            uint priority = DefaultPriority,
            BaseMixedRealityProfile profile = null) : base(spatialAwarenessSystem, name, priority, profile)
        {
            ReadProfile();
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Enable()
        {
            base.Enable();
            makingPlanes = false;
            ActivePlanes = new List<GameObject>();
            planesParent = new GameObject("SurfacePlanes");
            planesParent.transform.position = Vector3.zero;
            planesParent.transform.rotation = Quaternion.identity;
        }

        // Update is called once per frame
        public override void Update()
        {
            if (!makingPlanes)
            {
                makingPlanes = true;
                // Processing the mesh can be expensive...
                // We use Coroutine to split the work across multiple frames and avoid impacting the frame rate too much.
                MakePlanes();
            }
        }

        /// <summary>
        /// Iterator block, analyzes surface meshes to find planes and create new 3D cubes to represent each plane.
        /// </summary>
        /// <returns>Yield result.</returns>
        private void MakePlanes()
        {

            float start = Time.realtimeSinceStartup;

            // Get the latest Mesh data from the Spatial Mapping Manager.
            List<PlaneFinding.MeshData> meshData = new List<PlaneFinding.MeshData>();
            List<MeshFilter> filters = new List<MeshFilter>();

            var spatialAwarenessSystem = CoreServices.SpatialAwarenessSystem;
            if (spatialAwarenessSystem != null)
            {
                GameObject parentObject = spatialAwarenessSystem.SpatialAwarenessObjectParent;

                // Loop over each observer
                foreach (MeshFilter filter in parentObject.GetComponentsInChildren<MeshFilter>())
                {
                    filters.Add(filter);
                }
            }


            for (int index = 0; index < filters.Count; index++)
            {
                MeshFilter filter = filters[index];
                if (filter != null && filter.sharedMesh != null)
                {
                    // fix surface mesh normals so we can get correct plane orientation.
                    filter.mesh.RecalculateNormals();
                    meshData.Add(new PlaneFinding.MeshData(filter));
                }
            }

            BoundedPlane[] planes = PlaneFinding.FindPlanes(meshData, snapToGravityThreshold, MinArea);
            CreateGameObjects(planes);
        }

        private void CreateGameObjects(BoundedPlane[] planes)
        {
            for (int index = 0; index < planes.Length; index++)
            {
                GameObject destinationPlane;
                BoundedPlane boundedPlane = planes[index];

                // Instantiate a SurfacePlane object, which will have the same bounds as our BoundedPlane object.
                destinationPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
                destinationPlane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
              
                destinationPlane.transform.parent = planesParent.transform;
                destinationPlane.layer = 31;

                ActivePlanes.Add(destinationPlane);
            }
        }

            // TODO: Read configuration from profile
            private void ReadProfile()
        {
            // TODO: ensure profile is correct type
        }
    }

}

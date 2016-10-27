using System;
using System.Collections;
using KSP.UI.Screens.Settings.Controls;
using UnityEngine;

namespace DynamicPressureAirShadow
{
    public class DynamicPressureAirShadow : PartModule, IModuleInfo
    {
        [KSPField] public double MaxDynamicPressure = -1;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "AirStream Protected")]//, isPersistant = true)]
        public string AirStreamProtected;

        public bool IsProtected;
        public bool IsProtectedBottom;

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Vector")]//, isPersistant = true)]
        public float ProtectionRate;

        private IEnumerator DynamicPressure()
        {
            while (HighLogic.LoadedSceneIsFlight)
            {
                yield return new WaitForSeconds(1);
                if (HighLogic.LoadedSceneIsFlight)
                    CheckDynamicPressure();
            }
        }

        private void Start()
        {
            if (HighLogic.LoadedSceneIsFlight)
                StartCoroutine(DynamicPressure());
        }

        private void Update()
        {
            Vector3 partSize;
            Vector3 partAttach = part.partTransform.localPosition;
            Vector3 parentSize;// = defendingParentPart.prefabSize;
            Part defendingParentPart; //new Part();

            if (part.parent == null) return;

            defendingParentPart = FindDefendingParentPart();

            if (HighLogic.LoadedSceneIsEditor)
            {
                partSize = part.partInfo.partPrefab.GetPartRendererBound().size;
                parentSize = defendingParentPart.partInfo.partPrefab.GetPartRendererBound().size;
                //ProtectionRate = 1;
            }
            else
            {
                partSize = part.DragCubes.WeightedSize;
                parentSize = defendingParentPart.DragCubes.WeightedSize;
            }

            if (!part.ShieldedFromAirstream) 
            {
                IsProtected = CheckProtectionFromParent(part, partAttach, partSize, parentSize, defendingParentPart);

                if (IsProtected)
                    AirStreamProtected = "Yes/No";
                else
                    AirStreamProtected = "No/No";

                if (part.parent == defendingParentPart && partAttach.y >= -partSize.y / 2 && IsProtected)
                {
                    AirStreamProtected = "No/Yes";
                }
            }
            else
            {
                IsProtected = true;
                AirStreamProtected = "Yes/Yes";
            }
        }

        private void CheckDynamicPressure()
        {
            if (vessel == null) return;
            if (!vessel.HoldPhysics && vessel.atmDensity > 0 && MaxDynamicPressure > 0)
            {
                if (vessel.dynamicPressurekPa > MaxDynamicPressure) // && !part.ShieldedFromAirstream)
                {
                    if (!IsProtected)//CheckProtectionFromParent())
                    {
                        ScreenMessages.PostScreenMessage(part.partInfo.title + " was ripped off by strong airflow.", 5f, ScreenMessageStyle.UPPER_CENTER);
                        string msg = string.Format(KSPUtil.PrintTimeStamp(FlightLogger.met), part.partInfo.title);
                        FlightLogger.eventLog.Add(string.Format("[{0}]: {1} was ripped off by strong airflow.", msg, part.partInfo.title));
                        part.decouple();                       
                    }
                }
            }
        }
     
        private bool CheckProtectionFromParent(Part thisPart, Vector3 partAttach, Vector3 partSize, Vector3 parentSize, Part defendingParentPart)
        {
            if (thisPart.parent == null) return false;
            bool K1 = false;
            bool K2 = false;

            if (HighLogic.LoadedSceneIsEditor)
                ProtectionRate = 1;
            else
            {
                if (vessel.Landed)
                    ProtectionRate = 1;
                else //in Flight
                {
                    Vector3 vesselAttitude = vessel.GetComponent<Rigidbody>().velocity.normalized;
                    Vector3 vesselUP = vessel.transform.up.normalized;
                    ProtectionRate = Vector3.Dot(vesselAttitude, vesselUP);
                    if ((ProtectionRate > 0 && vessel.verticalSpeed > 0) || (ProtectionRate < 0 && vessel.verticalSpeed < 0))
                        K1 = true;
                    if ((ProtectionRate > 0 && vessel.verticalSpeed > 0) || (ProtectionRate < 0 && vessel.verticalSpeed > 0) ||
                        (ProtectionRate > 0 && vessel.verticalSpeed < 0))
                        K2 = true;

                    if (
                        (vesselAttitude.x < 0 && vesselAttitude.y < 0 && ProtectionRate > 0 && vessel.verticalSpeed > 0) ||
                        (vesselAttitude.x > 0 && vesselAttitude.y < 0 && ProtectionRate < 0 && vessel.verticalSpeed > 0) ||
                        (vesselAttitude.x > 0 && vesselAttitude.y > 0 && ProtectionRate > 0 && vessel.verticalSpeed < 0)
                        )
                    {
                        ProtectionRate = Math.Abs(ProtectionRate) * 1;
                    } 
                    else
                    {
                        ProtectionRate = Math.Abs(ProtectionRate) * -1;
                    }
                }
            }

            var partAttachX = Math.Abs(partAttach.x)* ProtectionRate + partSize.x;
            var partAttachY = -partAttach.y * ProtectionRate + partSize.y;
            var partAttachZ = Math.Abs(partAttach.z) * ProtectionRate + partSize.z;

            var checkX = partAttachX < parentSize.x / 2;
            var checkY = partAttachY < parentSize.y / 2;
            var checkZ = partAttachZ < parentSize.z / 2;

            IsProtected = checkX & checkY & checkZ;

            if (part.parent == defendingParentPart && partAttach.y >= -partSize.y/2)
            {
                if (K1 && !IsProtected)
                    IsProtected = true;
                if (K2 && IsProtected)
                    IsProtected = false;
            }

            return IsProtected;
        }

        private Part FindDefendingParentPart()
        {
            Part defendingParentPart = part.parent;
            Part p1 = part.parent;
            Part p2 = p1.parent;
            do
            {
                if (p2 == null) break;
                if (part.originalStage == p2.originalStage)
                {
                    if (!HighLogic.LoadedSceneIsEditor)
                    {
                        defendingParentPart = (p1.DragCubes.WeightedSize.x + p1.DragCubes.WeightedSize.z)/2
                                              <
                                              (p2.DragCubes.WeightedSize.x + p2.DragCubes.WeightedSize.z)/2
                            ? p2
                            : p1;
                    }
                    else
                    {
                        defendingParentPart = (p1.partInfo.partPrefab.GetPartRendererBound().size.x +
                                               p1.partInfo.partPrefab.GetPartRendererBound().size.z)/2
                                              <
                                              (p2.partInfo.partPrefab.GetPartRendererBound().size.x +
                                               p2.partInfo.partPrefab.GetPartRendererBound().size.z)/2
                            ? p2
                            : p1;
                    }
                    p1 = p2;
                }
                else break;
            } while (p1.parent != null);
            return defendingParentPart;
        }

        public string GetModuleTitle()
        {
            return "Max dynamic pressure";
        }

        public override string GetInfo()
        {
            return MaxDynamicPressure.ToString();
        }

        public Callback<Rect> GetDrawModulePanelCallback()
        {
            return null;
        }

        public string GetPrimaryField()
        {
            return "<b><color=#ff00ffff>Can be damaged if Dynamic pressure becomes too high</color></b>";
        }

        public static string FormatTimestamp(int years, int days, int hours, int minutes, int seconds)
        {
            return string.Format("{0:D2}:{1:D2}:{2:D2}", hours, minutes, seconds);
        }
    }
}


using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;
using System;
using Base.Management;
using Base.Scriptables;
using Base.Utils;

namespace Base.ObjectPooling
{
    public class ObjectCamp:MonoBehaviour
    {
        public readonly static List<ObjectStock> objectStocks;
        public readonly static List<TypePrefabRegisterMap> typePrefabRegisterMaps;
        public readonly static List<TypeScriptableObjectMap> typeScriptableObjectMaps;
        public readonly static List<ManagerBase> managers;

        public static Transform PassiveObjectHolder;
        static ObjectCamp()
        {
            if (typePrefabRegisterMaps == null) typePrefabRegisterMaps = new List<TypePrefabRegisterMap>();
            if (objectStocks == null) objectStocks = new List<ObjectStock>();
            if (typeScriptableObjectMaps == null) typeScriptableObjectMaps = new List<TypeScriptableObjectMap>();
            if (managers == default) managers = new List<ManagerBase>();
        }
        private void Awake()
        {
            PassiveObjectHolder = transform;
        }

        private void OnDestroy()
        {
            objectStocks.Clear();
            typePrefabRegisterMaps.Clear();
            typeScriptableObjectMaps.Clear();
            managers.Clear();
        }
        public static void PushObject<T>(T Object ,string _variation="" ) where T : IObjectCampMember 
        {
            if (Object==null) return;
            if (!(Object is MonoBehaviour)) return;
            var Finded=objectStocks.Find(x =>x.CanPushObject(typeof(T)));
            if (Finded == null)
            {
                Finded = new ObjectStock();
                Finded.Objects.Add(new ObjectStock.StockObjectItem() { ObjectCampMember = Object, variation = _variation });
                objectStocks.Add(Finded);
            }
            else
            {
                if (Finded.IsObjectInPool(Object))
                {
                    throw new Exceptions.ObjectAlreadyInObjectCampException((Object as MonoBehaviour).transform.name);
                }

                Finded.Objects.Add(new ObjectStock.StockObjectItem { variation=_variation, ObjectCampMember=Object });
            }
            MonoBehaviour behaviour = Object as MonoBehaviour;
            behaviour.gameObject.SetActive(false);
            if(PassiveObjectHolder)
            behaviour.transform.SetParent(PassiveObjectHolder);
        }
        public static async Task<T> PullObject<T>(string variation="",Transform carrier=null) where T : IObjectCampMember
        {
            var Finded = objectStocks.Find(x =>  x.CanPullObject(typeof(T),variation));
            if (Finded != null)
            {
                var Object= (T)Finded.GetObject(variation);
                MonoBehaviour behaviour = Object as MonoBehaviour;
                behaviour.gameObject.SetActive(true);
                behaviour.transform.SetParent(carrier);
                return Object;
            }
            else
            {
                var findedRegisterMap= typePrefabRegisterMaps.Find(x => x.type == typeof(T)&&x.variation.Equals(variation,StringComparison.OrdinalIgnoreCase));
                if (findedRegisterMap == null)
                    throw new Exceptions.ObjectTypePrefabNotRegisteredException(typeof(T));
                
                var task = findedRegisterMap.assetReference.AddressableInstantiate(carrier);
                await task;
                task.Result.TryGetComponent(out T Component);
                MonoBehaviour behaviour = Component as MonoBehaviour;
                behaviour.gameObject.SetActive(true);
                return Component;
            }
           
        }
        public static void RegisterPrefab(TypePrefabRegisterMap typePrefabRegisterMap)
        {
            if (!typePrefabRegisterMap.type.GetInterfaces().Any(x => x == typeof(IObjectCampMember)))
                throw new Exceptions.ObjectCampRegisterException(typePrefabRegisterMap.type,0);
            if (typePrefabRegisterMaps.Any(x => x.type == typePrefabRegisterMap.type&&x.variation.Equals(typePrefabRegisterMap.variation, System.StringComparison.OrdinalIgnoreCase)))
                throw new Exceptions.ObjectCampRegisterException(typePrefabRegisterMap.type,1);
            typePrefabRegisterMaps.Add(typePrefabRegisterMap);
        }
        public static void RegisterScriptable<T>(T gameSO,string variation="") where T: GameSOBase
        {
            Type type = gameSO.GetType();
            if (typeScriptableObjectMaps.Any(x=>x.type==type||x.gameSO==gameSO)) throw new Exceptions.ObjectCampRegisterException(type, 1);
            typeScriptableObjectMaps.Add(new TypeScriptableObjectMap(type, gameSO,variation));
        }
        public static T PullScriptable<T>(string variation="") where T: GameSOBase
        {
            Type type = typeof(T);
            var finded = typeScriptableObjectMaps.Find(x => x.type == type&&x.variation==variation);
            if (finded == null) return default;
            return finded.gameSO as T;
        }
        public static void RegisterManager(ManagerBase manager) 
        {
            if (managers.Any(x => x == manager)) return;
            managers.Add(manager);
        }

        public static T PullManager<T>() where T : ManagerBase
        {
            var data = managers.Find(x => x is T);
            if (!data) return default;
            return data as T;
        }
    }
}


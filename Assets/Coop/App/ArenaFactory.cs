using UnityEngine;

namespace Coop.App
{
    public static class ArenaFactory
    {
        public static void Create()
        {
            if (GameObject.Find("ArenaGeometry")) return;
            var root = new GameObject("ArenaGeometry").transform;
            Box(root,"Floor",new Vector3(0,-.5f,0),new Vector3(30,1,30),new Color(.13f,.19f,.23f));
            Box(root,"North",new Vector3(0,1,15),new Vector3(30,3,1),new Color(.23f,.34f,.38f));
            Box(root,"South",new Vector3(0,1,-15),new Vector3(30,3,1),new Color(.23f,.34f,.38f));
            Box(root,"East",new Vector3(15,1,0),new Vector3(1,3,30),new Color(.23f,.34f,.38f));
            Box(root,"West",new Vector3(-15,1,0),new Vector3(1,3,30),new Color(.23f,.34f,.38f));
            Box(root,"Table",new Vector3(0,.5f,5),new Vector3(5,1,2),new Color(.7f,.45f,.2f));
            for (int i=0;i<5;i++) Box(root,"Step",new Vector3(-8,i*.25f,2+i),new Vector3(3,.5f,1),new Color(.35f,.45f,.5f));
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.3f; light.transform.rotation=Quaternion.Euler(50,-30,0);
            RenderSettings.ambientLight = new Color(.5f,.55f,.6f);
        }
        static void Box(Transform root,string name,Vector3 position,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(root); go.transform.position=position; go.transform.localScale=scale;
            go.GetComponent<Renderer>().material.color=color;
        }
    }
}

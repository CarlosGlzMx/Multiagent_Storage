/* Client GC
Adapted by [Jorge Cruz](https://jcrvz.co) for:
TC2008B. Sistemas Multiagentes y Gráficas Computacionales. Tecnológico de Monterrey.

Revised version, Nov. 2021
Original implementation: C# client to interact with Unity, Sergio Ruiz, Jul. 2021
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class AgentInfo
{
    public string kind;
    public string colour;
    public Vector3 position;

    public AgentInfo(string kind, string colour, Vector3 position)
    {
        this.kind = kind;
        this.colour = colour;
        this.position = position;
    }
    
    public static AgentInfo CreateFromJson(string jsonString)
    {
        return JsonUtility.FromJson<AgentInfo>(jsonString);
    }

    public string SaveToString()
    {
        return JsonUtility.ToJson(this);
    }
}

public class AgentController : MonoBehaviour
{
    private List<List<AgentInfo>> _features;
    public GameObject agent1Prefab; // Green ball
    public GameObject agent2Prefab; // Red ball

    public int clonesOfAgent1 = 15; // How many green balls we have
    public int clonesOfAgent2 = 15; // How many red balls we have

    private GameObject[] _agents;
    public float timeToUpdate = 5.0f;
    private float _timer;
    private float _dt;
    

    // IEnumerator - yield return
    private IEnumerator SendData(string data)
    {
        WWWForm form = new WWWForm();
        form.AddField("bundle", "the data");
        //string url = "http://localhost:8585";
        string url = "http://5519-35-245-218-31.ngrok.io/";
        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(data);
            www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            //www.SetRequestHeader("Content-Type", "text/html");
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();          // Talk to Python
            if ((www.result == UnityWebRequest.Result.ConnectionError) || 
                (www.result == UnityWebRequest.Result.ProtocolError))
            {
                Debug.Log(www.error);
            }
            else
            {
                //Debug.Log(www.downloadHandler.text);    // Answer from Python
                //Debug.Log("Form upload complete!");
                //Data tPos = JsonUtility.FromJson<Data>(www.downloadHandler.text.Replace('\'', '\"'));
                //Debug.Log(tPos);
                string txt = www.downloadHandler.text.Replace('\'', '\"');
                txt = txt.TrimStart('"', '{', 'd', 'a', 't', 'a', ':', '[');
                txt = "{\"" + txt;
                txt = txt.TrimEnd(']', '}');
                txt = txt + '}';
                string[] strs = txt.Split(new string[] { "}, {" }, StringSplitOptions.None);
                Debug.Log("strs.Length:" + strs.Length);
                
                var newAgentInfo = new List<AgentInfo>();
                for (int i = 0; i < strs.Length; i++)
                {
                    strs[i] = strs[i].Trim();
                    if (i == 0) strs[i] = strs[i] + '}';
                    else if (i == strs.Length - 1) strs[i] = '{' + strs[i];
                    else strs[i] = '{' + strs[i] + '}';
                    
                    var agentInfo = AgentInfo.CreateFromJson(strs[i]);
                    //Vector3 test = JsonUtility.FromJson<Vector3>(strs[i]);
                    
                    newAgentInfo.Add(agentInfo);
                }
                
                /*
                List<Vector3> feature = new List<Vector3>();
                for(int s = 0; s < _agents.Length; s++)
                {
                    //spheres[s].transform.localPosition = newPositions[s];
                    feature.Add(newPositions[s]);
                }*/
                _features.Add(newAgentInfo);
            }
        }

    }

    // Start is called before the first frame update
    void Start()
    {
        int numOfAgents = clonesOfAgent1 + clonesOfAgent2;
        _agents = new GameObject[numOfAgents];
        for(int i = 0; i < numOfAgents; i++)
        {
            if(i < clonesOfAgent1)
            {
                _agents[i] = Instantiate(agent1Prefab, Vector3.zero, 
                    Quaternion.Euler(0, 0, -90));
            }
            else
            {
                _agents[i] = Instantiate(agent2Prefab, Vector3.zero, 
                    Quaternion.identity);
            }
        }


        _features = new List<List<AgentInfo>>();
        Debug.Log(_agents.Length);
#if UNITY_EDITOR
        //string call = "WAAAAASSSSSAAAAAAAAAAP?";
        Vector3 fakePos = new Vector3(3.44f, 0, -15.707f);
        AgentInfo fakeAgent = new AgentInfo("Ball", "Red", fakePos);
        string json = fakeAgent.SaveToString();
        StartCoroutine(SendData(json));
        //Debug.Log(json);
        _timer = timeToUpdate;
#endif
    }

    // Update is called once per frame
    void Update()
    {
        /*
         *    5 -------- 100
         *    timer ----  ?
         */
        _timer -= Time.deltaTime;
        _dt = 1.0f - (_timer / timeToUpdate);

        if(_timer < 0)
        {
#if UNITY_EDITOR
            _timer = timeToUpdate; // reset the timer
            Vector3 fakePos = new Vector3(3.44f, 0, -15.707f);
            AgentInfo fakeAgent = new AgentInfo("Ball", "Red", fakePos);
            string json = fakeAgent.SaveToString();
            StartCoroutine(SendData(json));
#endif
        }


        if (_features.Count > 1)
        {
            for (int s = 0; s < _agents.Length; s++)
            {
                // Get the last position for the s-th agent
                List<AgentInfo> last = _features[^1];
                Debug.Log(last[s].position);
                
                // Get the previous to last position for the s-th agent
                List<AgentInfo> prevLast = _features[^2];
                
                // Interpolate using dt
                Vector3 interpolated = Vector3.Lerp(
                    prevLast[s].position, last[s].position, _dt);
                Vector3 dir = last[s].position - prevLast[s].position;
                
                //_agents[s].transform.localPosition = interpolated;
                //_agents[s].transform.rotation = Quaternion.LookRotation(dir);
                GameObject prefabToUse = new GameObject();
                switch (last[s].colour)
                {
                    case "Red" when last[s].kind == "Ball":
                        prefabToUse = agent1Prefab;
                        break;
                    case "Green" when last[s].kind == "Ball":
                        prefabToUse = agent2Prefab;
                        break;
                    default:
                        Debug.Log("No defined Prefab");
                        break;
                }
                Destroy(_agents[s]);
                _agents[s] = Instantiate(prefabToUse, interpolated,
                    Quaternion.LookRotation(dir));
            }
        }
    }
}

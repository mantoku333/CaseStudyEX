using UnityEngine;

namespace Metroidvania.Testing
{
    /// <summary>
    /// 時間（Time.timeScale）が止まっているかを確認するためのテストスクリプト。
    /// 指定された半径と速度で円を描くように移動し続けます。
    /// 時間が止まればピタッと止まり、動けばまた動き出します。
    /// </summary>
    public class TimeScaleTestCircle : MonoBehaviour
    {
        [Header("円運動の設定")]
        [Tooltip("円の半径")]
        public float radius = 2f;
        
        [Tooltip("回転するスピード（数値が大きいほど速い）")]
        public float speed = 3f;

        private Vector3 _startPosition;
        private float _angle;

        private void Start()
        {
            // 初期位置を円の中心として記憶する
            _startPosition = transform.position;
        }

        private void Update()
        {
            // Time.deltaTime が関わっているため、Time.timeScale = 0 のときは _angle が増えず、動きが止まります
            _angle += speed * Time.deltaTime;

            // 円運動の計算（CosでX軸、SinでY軸）
            float x = Mathf.Cos(_angle) * radius;
            float y = Mathf.Sin(_angle) * radius;

            // 記憶した初期位置を中心に円を描く
            transform.position = _startPosition + new Vector3(x, y, 0);
        }
    }
}

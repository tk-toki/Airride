﻿using System;
using System.Collections.Generic;
using UnityEngine;

public class Machine : Control
{
    #region const
    const float chargeDashPossible = 0.75f; //チャージダッシュ可能量
    const float exitMachineVertical = -0.9f; //降車時スティック最低入力量
    const float chargeUnderPower = 50000.0f; //charge中に下に加える力
    const float flyWeightMag = 100f; //滑空時の落下倍率
    const float flyChargeSpeed = 1500f; //滑空中の自動チャージ速度分率
    const float dashBoardMag = 2.5f; //ダッシュボード倍率
    const float boundPower = 200f; //跳ね返る力
    const float GetOffPossibleTime = 2.0f; //乗車してから降車可能までの時間
    public const float limitStatus = 16; //ステータス下限上限
    #endregion

    #region Serialize
    [SerializeField] private bool debug = false;
    [SerializeField] private MachineStatus status;
    [SerializeField] private DebugText dText;
    [SerializeField] private StateManager state;
    #endregion

    #region 変数
    protected Player player;
    private List<float> getItemList = new List<float>();    //アイテムの取得状態
    private List<float> statusList = new List<float>();    //ステータスのバフ状態
    private float speed = 0; //現在の速度
    private float chargeAmount = 1; //チャージ量
    private float rideTime = 0; //乗車開始してからの時間
    private bool nowBrake = false; //charge中かどうか
    private bool onGround = true; //接地フラグ
    private bool bound = false; //跳ね返り処理を行うフラグ
    private bool getOffPossible = false;
    private Vector3 chargePos;
    #endregion

    #region プロパティ
    public Player Player
    {
        set
        {
            player = value;
        }
    }
    public MachineStatus MachineStatus
    {
        get
        {
            return status;
        }
    }
    public float SaveSpeed { get; private set; } = 0;
    #endregion

    #region public
    #region Control
    public override void Controller()
    {
        if (state.State == StateManager.GameState.Game)
        {
            Move();
            RideTimeCount();
            if (getOffPossible)
            {
                GetOff();
            }
            rbody.constraints = RigidbodyConstraints.FreezeRotation;
        }
        else
        {
            //チャージのみ可能
            if (InputManager.Instance.InputA(InputType.Hold))
            {
                Charge();
            }
            else if (InputManager.Instance.InputA(InputType.Up))
            {
                chargeAmount = 1;
            }
            rbody.constraints = RigidbodyConstraints.FreezeAll;
        }

        if (debug)
        {
            DebugDisplay();
        }
    }

    public override void FixedController()
    {
        //移動量
        if (nowBrake)
        {
            rbody.velocity = chargePos * speed;
        }
        else
        {
            rbody.velocity = transform.forward * speed;
        }

        //空中時の処理
        if (!onGround)
        {
            rbody.AddForce(Vector3.down * Status(StatusType.Weight) * flyWeightMag);
            //チャージ中の下に力を入れる処理
            if (nowBrake)
            {
                rbody.AddForce(Vector3.down * chargeUnderPower);
            }
        }
    }
    #endregion

    #region アイテム処理
    /// <summary>
    /// 取得したアイテムのカウント
    /// </summary>
    /// <param name="item">変動させるステータス</param>
    /// <param name="mode">変更値</param>
    /// <returns>上限下限か</returns>
    public bool ItemCount(ItemName item, ItemMode mode)
    {
        switch (mode)
        {
            //バフアイテム
            case ItemMode.Buff:
                if (mode == ItemMode.Buff)
                {
                    //上限チェック
                    if (getItemList[(int)item] < limitStatus)
                    {
                        getItemList[(int)item]++; //アイテムの取得数を増やす
                        return false;
                    }
                }
                return true;
            //デバフアイテム
            case ItemMode.Debuff:
                //下限チェック
                if (getItemList[(int)item] > -limitStatus)
                {
                    getItemList[(int)item]--; //アイテム取得数を減らす
                    return false;
                }
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// ステータスの変動
    /// </summary>
    /// <param name="name">変動させるステータス</param>
    /// <param name="up">上昇か下降か</param>
    public void ChangeStatus(StatusType name, ItemMode mode, float mag = 0)
    {
        bool plus;
        //Statusが基準ステータスより高かったら
        if(statusList[(int)name] <= status.GetStatus(name, MachineStatus.Type.Default))
        {
            //計算基準をPlus値で行う
            plus = true;
        }
        else
        {
            plus = false;
        }

        if (plus)
        {
            //ステータスを上昇
            if (mode == ItemMode.Buff)
            {
                statusList[(int)name] += status.PlusStatus(name, mag);
            }
            //ステータスを下降
            else
            {
                statusList[(int)name] -= status.PlusStatus(name, mag);
            }
        }
        else
        {
            if (mode == ItemMode.Buff)
            {
                statusList[(int)name] += status.MinusStatus(name, mag);
            }
            else
            {
                statusList[(int)name] -= status.MinusStatus(name, mag);
            }
        }
    }
    #endregion

    #region SpeedMater
    /// <summary>
    /// SpeedMaterに表示するSpeed
    /// </summary>
    /// <returns>Textに表示するSpeed</returns>
    public string SpeedMaterText()
    {
        float moveSpeed = rbody.velocity.magnitude;
        float intSpeed = moveSpeed - moveSpeed % 1; //整数部分のみ抽出
        float fewSpeed = moveSpeed % 1; //小数部分のみ抽出

        return intSpeed.ToString("000")
            + "\n"
            + "<size=30>"
            + fewSpeed.ToString(".00")
            + "</size>";
    }

    /// <summary>
    /// 正規化したチャージ量
    /// </summary>
    /// <returns>チャージ量</returns>
    public float NormalizeCharge()
    {
        //0~1の範囲に正規化
        float charge = (chargeAmount - 1) / (Status(StatusType.Charge) - 1);
        return charge;
    }
    #endregion

    #region other
    /// <summary>
    /// 壁や、アイテムボックスにぶつかった時に跳ね返る処理
    /// </summary>
    public void Bound()
    {
        SaveSpeed = speed;
        speed /= 2;
        rbody.AddRelativeForce(
            (-Vector3.forward * SaveSpeed * boundPower) 
            / Status(StatusType.Weight),
            ForceMode.Impulse);
    }

    public float Status(StatusType name)
    {
        return statusList[(int)name];
    }
    #endregion
    #endregion

    #region protected
    protected override void Move()
    {
        base.Move();

        //Aボタンを押している
        if (InputManager.Instance.InputA(InputType.Hold))
        {
            Charge();
            Brake();
        }
        //Aボタンを離した
        else if (InputManager.Instance.InputA(InputType.Up))
        {
            ChargeDash();
        }
        //Aボタンを押していない
        else
        {
            Accelerator();
        }

        transform.Rotate(0, horizontal * Status(StatusType.Turning) * Time.deltaTime, 0);
    }

    /// <summary>
    /// チャージ処理
    /// </summary>
    protected virtual void Charge()
    {
        //チャージ
        if (Status(StatusType.Charge) > chargeAmount)
        {
            chargeAmount += Status(StatusType.ChargeSpeed) * Time.deltaTime;
        }
    }

    /// <summary>
    /// ブレーキ処理
    /// </summary>
    protected virtual void Brake()
    {
        if (!nowBrake)
        {
            chargePos = transform.forward;
            nowBrake = true;
        }

        //ブレーキ
        if (speed - Status(StatusType.Brake) * Time.deltaTime > 0)
        {
            speed -= Status(StatusType.Brake) * Time.deltaTime;
        }
        else
        {
            speed = 0;
        }
    }

    /// <summary>
    /// チャージダッシュ
    /// </summary>
    protected virtual void ChargeDash()
    {
        //チャージダッシュはチャージが一定以上でなければ発動しない
        if (chargeAmount >= Status(StatusType.Charge) * chargeDashPossible)
        {
            speed += Status(StatusType.Acceleration) * chargeAmount;
        }
        //チャージ量をリセット
        chargeAmount = 1;
        nowBrake = false;
    }

    /// <summary>
    /// 通常加速処理
    /// 最高速度以上の場合の減速処理も含む
    /// </summary>
    protected virtual void Accelerator()
    {
        //最高速度
        float max = Status(StatusType.MaxSpeed);
        float maxSpeed = max;
        //地面に接地していないときの処理
        if (!onGround)
        {
            maxSpeed = Status(StatusType.FlySpeed);
            //徐々にチャージがたまる
            chargeAmount += Status(StatusType.ChargeSpeed) / flyChargeSpeed;
        }

        //Maxスピードオーバーの許容範囲
        float tolerance = 1.0f;

        if (nowBrake)
        {
            nowBrake = false;
        }

        if (maxSpeed  > speed)
        {
            //自動加速
            speed += Status(StatusType.Acceleration) * Time.deltaTime;
        }
        else if (maxSpeed + tolerance > speed)
        {
            //速度を一定に保つ
            speed = maxSpeed;
        }
        else
        {
            if (speed > 999)
            {
                speed = 999;
            }
            //徐々に速度を落とす
            speed -= Status(StatusType.Brake) * Time.deltaTime;
        }
    }

    /// <summary>
    /// 降車処理
    /// </summary>
    protected override void GetOff()
    {
        //スティック下+Aボタンかつ接地しているとき
        if (InputManager.Instance.InputA(InputType.Down) 
            && vertical < exitMachineVertical 
            && onGround)
        {
            //入力値のリセット
            vertical = 0;
            horizontal = 0;
            //乗車時間のリセット
            rideTime = 0;
            //speedを初期化
            speed = 0;
            //親子関係の解除
            transform.parent = null;
            //PlayerのConditionをMachineからHumanに
            player.PlayerCondition = Player.Condition.Human;
            //マシンの割り当てを削除
            player.Machine = null;
            player = null;
            return;
        }
    }
    #endregion

    #region private
    protected virtual void Start()
    {
        //ステータス倍率リストの初期化
        Array statusType = Enum.GetValues(typeof(StatusType));
        for(int i = 0; i < statusType.Length; i++)
        {
            statusList.Add(status.StartStatus((StatusType)i)); //初期値
        }

        //アイテム取得リストの初期化
        var itemType = Enum.GetValues(typeof(ItemName));
        foreach(var item in itemType)
        {
            getItemList.Add(-2); //初期値は-2
        }
    }

    /// <summary>
    /// 降車可能時間のカウントとフラグ管理
    /// </summary>
    private void RideTimeCount()
    {
        if(rideTime < GetOffPossibleTime)
        {
            rideTime += Time.deltaTime;
            getOffPossible = false;
        }
        else
        {
            getOffPossible = true;
        }
    }

    /// <summary>
    /// デバッグテキスト処理
    /// </summary>
    private void DebugDisplay()
    {
        dText.Debug(DebugText.Position.Right,
            "GET ITEM"
            + "\nMaxSpeed : " + getItemList[0]
            + "\nAcceleration : " + getItemList[1]
            + "\nTurning : " + getItemList[2]
            + "\nCharge : " + getItemList[3]
            + "\nWeight : " + getItemList[4]
            + "\nFly : " + getItemList[5]
            + "\nAll : " + getItemList[6],
            player);

        dText.Debug(DebugText.Position.Left,
            "STATUS"
            + "\nMaxSpeed : " + statusList[0]
            + "\nAcceleration : " + statusList[1]
            + "\nTurning : " + statusList[2]
            + "\nBrake : " + statusList[3]
            + "\nCharge : " + statusList[4]
            + "\nChargeSpeed : " + statusList[5]
            + "\nWeight : " + statusList[6]
            + "\nFlySpeed : " + statusList[7],
            player);
    }

    /// <summary>
    /// マシン影響オブジェクトに接触した際の処理
    /// </summary>
    /// <param name="other">接触した物体</param>
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Item")
        {
            Item item = other.gameObject.GetComponent<Item>();
            item.CatchItem(this); //入手したときの処理
        }

        if (other.gameObject.tag == "InfluenceObject")
        {
            //ダッシュボードに触れたときの速度に倍率をかける
            speed *= dashBoardMag;
        }
    }

    /// <summary>
    /// 接地判定
    /// </summary>
    /// <param name="collision">地面</param>
    private void OnCollisionStay(Collision collision)
    {
        if(collision.transform.tag == "StageObject" || collision.transform.tag == "NotBackSObject")
        {
            onGround = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.transform.tag == "StageObject" || collision.transform.tag == "NotBackSObject")
        {
            onGround = false;
        }
    }
    #endregion
}
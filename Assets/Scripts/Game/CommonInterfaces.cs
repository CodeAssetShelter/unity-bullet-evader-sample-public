using UnityEngine;
using Fusion;

public interface IGamePlayerMove
{
    public void Move(PlayerInputBase _input);
}

public interface IGameRegisterPlayer
{
    public void RegisterPlayer(GameObject _obj);
}

public interface IGameDestroyPlayer
{
    public void DestroyPlayer(PlayerRef _playerRef);
}

// 향후 공통 조작 필요하면 아래 샘플을 중심으로 사용
public interface IGamePlayerButton
{
    public void Pressed(PlayerInputBase _input);
}

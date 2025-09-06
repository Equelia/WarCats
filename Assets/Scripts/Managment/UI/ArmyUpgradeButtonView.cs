using UnityEngine;
using UnityEngine.UI;

public class ArmyUpgradeButtonView : MonoBehaviour
{
	[Header("Refs")]
	[SerializeField] private Button button;       
	[SerializeField] private GameObject arrowIcon;   

	[Header("Rules")]
	[SerializeField] private int showArrowAtLevel = 2; 
	[SerializeField] private int maxArmyLevel = 3;     

	private IArmyEconomy _army;

	public void Bind(IArmyEconomy army)
	{
		_army = army;
		if (_army == null) return;

		_army.OnLevelChanged += HandleArmyLevelChanged;
		Refresh();
	}

	private void OnDestroy()
	{
		if (_army != null)
			_army.OnLevelChanged -= HandleArmyLevelChanged;
	}

	private void HandleArmyLevelChanged(int lvl) => Refresh();

	private void Refresh()
	{
		if (_army == null) return;

		int lvl = _army.State.level;

		// стрелка включается начиная с 2 уровня и скрывается на max уровне
		if (arrowIcon)
			arrowIcon.SetActive(lvl >= showArrowAtLevel && lvl < maxArmyLevel);

		// на максимальном уровне выключаем весь объект с кнопкой
		if (lvl >= maxArmyLevel)
			gameObject.SetActive(false);
		else
			gameObject.SetActive(true);
	}
}
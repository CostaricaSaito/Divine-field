$path = "C:/Users/コスタリカ斎藤/Divine2/Assets/Scripts/Battle/UI/BattleUIManager.cs"
$b = [System.IO.File]::ReadAllBytes($path)
"{0:X2} {1:X2} {2:X2}" -f $b[0], $b[1], $b[2]

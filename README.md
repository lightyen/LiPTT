# LiPTT



#### 這是一個關於用UWP來實作PTT瀏覽器的一個練習，只是閒暇之餘消遣用。

#### 原本我一直都是用手機JPTT來上站的，但是想在Windows 10下就沒有好用的Store App。(我覺得MoPTT字對我來說太小或者沒有自動載入圖片的功能，而且開發者好像也沒在關注Win10，被放生了哭哭。)



<img src="https://i.imgur.com/Bghj8fU.png"></img>





> ## 相依性

- [SharpDX](https://github.com/sharpdx/SharpDX)

  在套用傳統的顯示介面時，底層用的是DirectX的DrawText功能，以前稍微有玩過SharpDX，就拿來用惹。

  ​



- Big5 和 Unicode轉換

  據說PTT用的編碼不是一開始的Big5，而是Big5-UAO，以前好像有聽說叫Unicode補完計畫什麼的，現在找不太到了，只好Google慢慢找，然後把轉換表寫成文字檔，然後生成一個Encoding物件方便轉換。

  ​

- VT100 escape codes與ASCII code

  這邊應該已經不太需要介紹了，就是一直查表查功能、查表查功能......

  ​

- [SSH.NET](https://github.com/sshnet/SSH.NET)

  本來是要用來做ssh連線的，但是後來發現我在公司不能用port 22來連，然後443也PTT也沒開，大概是許多公用場所都沒開放吧，所以目前ssh的功能先放著，以後再說。(目前是TCP 連 port 443)

  ​

- [Newtonsoft.json](https://github.com/JamesNK/Newtonsoft.Json)

  為了可以把CSharp物件存在應用程式的設定檔，用了SerializeObject和DeserializeObject的功能。但是後來發現我並沒有很常碰到要把物件存起來的情況，一般用基本的型別就能搞定一切，所以以後會不會用還是個未知數，先放著。

  ​

- [TSF](https://msdn.microsoft.com/zh-tw/library/windows/desktop/ms629032(v=vs.85).aspx)

  關於中文輸入法的問題我還在研究，先放著。(謎：你富樫？)

  ​

- [UWP](https://docs.microsoft.com/en-us/uwp/api/)

  Universal Windows Platform、通用Windows平台，或者你可以叫他Windows Store App...

  管他叫什麼，反正就是微軟的一個應用程式開發平台，只是因為底層架構已經大改，舊版Windows還尚未支援。(應該是不會支援了，快點把你的Win7丟掉比較實際。)

  在WPF時期，最常用的Windows.System這個namespace，到了UWP下已經替換成Windows.UI.Xaml了。

  雖然開發的觀念基本上沒什麼大改，很多都能沿用，所以從WPF轉換過來的開發者應該很快就能上手。

  ​

回想起前半年，我還只是個WPF的初學者，後來因為吃飽太閒，玩了一下Store App後發現我覺得這有點屌，所以我也要來開發看看．．．(!?)

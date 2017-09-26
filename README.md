# LiPTT



#### 這是一個關於用UWP來實作PTT閱讀器的一個練習題，只是閒暇之餘消遣用。

#### 原本我一直都是用手機JPTT來上站的，但是想在Windows 10下就沒有好用的Store App，所以就開了這項目，在探索UWP之餘也能做些小成果。

#### (別跟我提MoPTT，我覺得不是很喜歡。)



<img src="https://i.imgur.com/1XRlB3j.png"></img>

<img src="https://i.imgur.com/RPSJQKG.png"></img>



```
找不到Nuget的話可以：方案總管=>方案(右鍵)=>還原Nuget套件，然後重開visual Studio
```



> ## 相依性

- [SharpDX](https://github.com/sharpdx/SharpDX)

  在套用傳統的顯示介面時，底層用的是DirectX的[DrawText](https://msdn.microsoft.com/en-us/library/windows/desktop/dd371919(v=vs.85).aspx)功能，以前稍微有玩過SharpDX，就拿來用了。

  實作這個主要的目的是拿來Debug用。

  ​



- [Big5](https://zh.wikipedia.org/wiki/%E5%A4%A7%E4%BA%94%E7%A2%BC) 和 [Unicode](http://graphemica.com/)轉換

  參考了[PTT](https://github.com/ptt/pttbbs)的source code，裡面有兩張table，一個是Big5轉Unicode，另一個是Unicode轉Big5，代號是[UAO250](https://moztw.org/docs/big5/)。

  ​

- VT100 escape codes與ASCII code

  這邊應該已經不太需要介紹了，就是一直查表查功能，在實作的時候隨手握著一張表是必須的。

  ​

- [SSH.NET](https://github.com/sshnet/SSH.NET)

  本來是要用來做ssh連線的，但是後來發現我在公司不能用SSH，不知道是不是port被擋還是甚麼被擋。大概是許多公用場所都沒開放吧，所以目前的SSH的功能測試進度先放著，以後再說。(目前我是直連 port 443)

  ​

- [Newtonsoft.json](https://github.com/JamesNK/Newtonsoft.Json)

  為了可以把CSharp物件存在應用程式的設定檔，用了SerializeObject和DeserializeObject的功能。但是後來發現我並沒有很常碰到要把物件存起來的情況，一般用基本的型別就能搞定一切，所以以後會不會用還是個未知數，先放著。

  ​

- [TSF](https://msdn.microsoft.com/zh-tw/library/windows/desktop/ms629032(v=vs.85).aspx)

  關於中文輸入法的問題我還在研究，繼續先放著。

  ​

- [UWP](https://docs.microsoft.com/zh-tw/windows/uwp/controls-and-patterns/)

  Universal Windows Platform、通用Windows平台，或者你可以叫他Windows Store App。

  管他叫什麼，反正就是微軟的一個應用程式開發平台，只是因為底層架構已經大改，舊版Windows還尚未支援。(應該是不會支援了，快點把你的Win7丟掉比較實際。)在WPF時期，最常用的Windows.System這個namespace，到了UWP下已經替換成Windows.UI.Xaml了。儘管如此，開發的觀念基本上沒什麼改變，所以從WPF轉換過來的開發者應該很快就能上手。需值得一提的是UWP很多功能還只是預覽版尚未釋出，希望微軟能快點放出來好讓我們有更多的玩具可以玩。

  ​

敝人也是個剛入門的程式新手，2016年的12月我連Windows API都還不知道是什麼碗糕，初嘗了Windows Form，接著開始寫了WPF的程式，到現在的UWP。我目前資歷太淺很菜雞，很多事我還是有所經驗不足，請各路大神們不吝嗇指教。(土下座拜師！)



## 未來想要實作or優化的功能

1. 我的最愛列表、動態磚
2. 文字Background color
3. 搜尋標題、作者、同標題之文章
4. 自動推文
5. UI設計
6. 歷史紀錄、推文追蹤
7. 文字與媒體自適應視窗大小
8. Youtube全螢幕
define(["libraryBrowser","mainTabsManager","components/categorysyncbuttons","cardBuilder","dom","apphost","imageLoader","playbackManager","scrollStyles","emby-itemscontainer","emby-tabs","emby-button"],function(libraryBrowser,mainTabsManager,categorysyncbuttons,cardBuilder,dom,appHost,imageLoader,playbackManager){"use strict";function enableScrollX(){return browserInfo.mobile}function getPortraitShape(){return enableScrollX()?"overflowPortrait":"portrait"}function getThumbShape(){return enableScrollX()?"overflowBackdrop":"backdrop"}function loadLatest(page,userId,parentId){var options={IncludeItemTypes:"Movie",Limit:18,Fields:"PrimaryImageAspectRatio,MediaSourceCount,BasicSyncInfo",ParentId:parentId,ImageTypeLimit:1,EnableImageTypes:"Primary,Backdrop,Banner,Thumb",EnableTotalRecordCount:!1};ApiClient.getJSON(ApiClient.getUrl("Users/"+userId+"/Items/Latest",options)).then(function(items){var allowBottomPadding=!enableScrollX(),container=page.querySelector("#recentlyAddedItems");cardBuilder.buildCards(items,{itemsContainer:container,shape:getPortraitShape(),scalable:!0,overlayPlayButton:!0,allowBottomPadding:allowBottomPadding})})}function loadResume(page,userId,parentId){var screenWidth=dom.getWindowSize().innerWidth,options={SortBy:"DatePlayed",SortOrder:"Descending",IncludeItemTypes:"Movie",Filters:"IsResumable",Limit:screenWidth>=1920?5:screenWidth>=1600?5:3,Recursive:!0,Fields:"PrimaryImageAspectRatio,MediaSourceCount,BasicSyncInfo",CollapseBoxSetItems:!1,ParentId:parentId,ImageTypeLimit:1,EnableImageTypes:"Primary,Backdrop,Banner,Thumb",EnableTotalRecordCount:!1};ApiClient.getItems(userId,options).then(function(result){result.Items.length?page.querySelector("#resumableSection").classList.remove("hide"):page.querySelector("#resumableSection").classList.add("hide");var allowBottomPadding=!enableScrollX(),container=page.querySelector("#resumableItems"),cardLayout=!1;cardBuilder.buildCards(result.Items,{itemsContainer:container,preferThumb:!0,shape:getThumbShape(),scalable:!0,overlayPlayButton:!0,allowBottomPadding:allowBottomPadding,cardLayout:cardLayout,showTitle:!0,showYear:!0,centerText:!cardLayout})})}function getRecommendationHtml(recommendation){var html="",title="";switch(recommendation.RecommendationType){case"SimilarToRecentlyPlayed":title=Globalize.translate("RecommendationBecauseYouWatched").replace("{0}",recommendation.BaselineItemName);break;case"SimilarToLikedItem":title=Globalize.translate("RecommendationBecauseYouLike").replace("{0}",recommendation.BaselineItemName);break;case"HasDirectorFromRecentlyPlayed":case"HasLikedDirector":title=Globalize.translate("RecommendationDirectedBy").replace("{0}",recommendation.BaselineItemName);break;case"HasActorFromRecentlyPlayed":case"HasLikedActor":title=Globalize.translate("RecommendationStarring").replace("{0}",recommendation.BaselineItemName)}html+='<div class="homePageSection">',html+='<h1 class="listHeader">'+title+"</h1>";var allowBottomPadding=!0;return enableScrollX()?(allowBottomPadding=!1,html+='<div is="emby-itemscontainer" class="itemsContainer hiddenScrollX">'):html+='<div is="emby-itemscontainer" class="itemsContainer vertical-wrap">',html+=cardBuilder.getCardsHtml(recommendation.Items,{shape:getPortraitShape(),scalable:!0,overlayPlayButton:!0,allowBottomPadding:allowBottomPadding}),html+="</div>",html+="</div>"}function loadSuggestions(page,userId,parentId){var screenWidth=dom.getWindowSize().innerWidth,url=ApiClient.getUrl("Movies/Recommendations",{userId:userId,categoryLimit:6,ItemLimit:screenWidth>=1920?8:screenWidth>=1600?8:screenWidth>=1200?6:5,Fields:"PrimaryImageAspectRatio,MediaSourceCount,BasicSyncInfo",ImageTypeLimit:1,EnableImageTypes:"Primary,Backdrop,Banner,Thumb"});ApiClient.getJSON(url).then(function(recommendations){if(!recommendations.length)return page.querySelector(".noItemsMessage").classList.remove("hide"),void(page.querySelector(".recommendations").innerHTML="");var html=recommendations.map(getRecommendationHtml).join("");page.querySelector(".noItemsMessage").classList.add("hide");var recs=page.querySelector(".recommendations");recs.innerHTML=html,imageLoader.lazyChildren(recs)})}function initSuggestedTab(page,tabContent){for(var containers=tabContent.querySelectorAll(".itemsContainer"),i=0,length=containers.length;i<length;i++)enableScrollX()?(containers[i].classList.add("hiddenScrollX"),containers[i].classList.remove("vertical-wrap")):(containers[i].classList.remove("hiddenScrollX"),containers[i].classList.add("vertical-wrap"))}function loadSuggestionsTab(view,params,tabContent){var parentId=params.topParentId,userId=Dashboard.getCurrentUserId();console.log("loadSuggestionsTab"),loadResume(tabContent,userId,parentId),loadLatest(tabContent,userId,parentId),loadSuggestions(tabContent,userId,parentId)}function getTabs(){return[{name:Globalize.translate("TabSuggestions")},{name:Globalize.translate("TabMovies")},{name:Globalize.translate("TabTrailers")},{name:Globalize.translate("TabCollections")},{name:Globalize.translate("TabGenres")},{name:Globalize.translate("TabStudios")}]}return function(view,params){function onBeforeTabChange(e){preLoadTab(view,parseInt(e.detail.selectedTabIndex))}function onTabChange(e){loadTab(view,parseInt(e.detail.selectedTabIndex))}function initTabs(){var tabsReplaced=mainTabsManager.setTabs(view,currentTabIndex,getTabs);if(tabsReplaced){var viewTabs=document.querySelector(".tabs-viewmenubar");viewTabs.addEventListener("beforetabchange",onBeforeTabChange),viewTabs.addEventListener("tabchange",onTabChange),libraryBrowser.configurePaperLibraryTabs(view,viewTabs,view.querySelectorAll(".pageTabContent"),[0,1,2,4,5]),viewTabs.triggerBeforeTabChange||viewTabs.addEventListener("ready",function(){viewTabs.triggerBeforeTabChange()})}}function getTabController(page,index,callback){var depends=[];switch(index){case 0:break;case 1:depends.push("scripts/movies");break;case 2:depends.push("scripts/movietrailers");break;case 3:depends.push("scripts/moviecollections");break;case 4:depends.push("scripts/moviegenres");break;case 5:depends.push("scripts/moviestudios")}require(depends,function(controllerFactory){var tabContent;0==index&&(tabContent=view.querySelector(".pageTabContent[data-index='"+index+"']"),self.tabContent=tabContent);var controller=tabControllers[index];controller||(tabContent=view.querySelector(".pageTabContent[data-index='"+index+"']"),controller=index?new controllerFactory(view,params,tabContent):self,tabControllers[index]=controller,controller.initTab&&controller.initTab()),callback(controller)})}function preLoadTab(page,index){getTabController(page,index,function(controller){renderedTabs.indexOf(index)==-1&&controller.preRender&&controller.preRender()})}function loadTab(page,index){currentTabIndex=index,getTabController(page,index,function(controller){renderedTabs.indexOf(index)==-1&&(renderedTabs.push(index),controller.renderTab())})}function onPlaybackStop(e,state){state.NowPlayingItem&&"Video"==state.NowPlayingItem.MediaType&&(renderedTabs=[],mainTabsManager.getTabsElement().triggerTabChange())}var self=this,currentTabIndex=parseInt(params.tab||"0");self.initTab=function(){var tabContent=view.querySelector(".pageTabContent[data-index='0']");categorysyncbuttons.init(tabContent),initSuggestedTab(view,tabContent)},self.renderTab=function(){var tabContent=view.querySelector(".pageTabContent[data-index='0']");loadSuggestionsTab(view,params,tabContent)};var tabControllers=[],renderedTabs=[];view.addEventListener("viewbeforeshow",function(e){if(initTabs(),!view.getAttribute("data-title")){var parentId=params.topParentId;parentId?ApiClient.getItem(Dashboard.getCurrentUserId(),parentId).then(function(item){view.setAttribute("data-title",item.Name),LibraryMenu.setTitle(item.Name)}):(view.setAttribute("data-title",Globalize.translate("TabMovies")),LibraryMenu.setTitle(Globalize.translate("TabMovies")))}var tabs=mainTabsManager.getTabsElement();tabs.triggerBeforeTabChange&&tabs.triggerBeforeTabChange()}),view.addEventListener("viewshow",function(e){Events.on(playbackManager,"playbackstop",onPlaybackStop),mainTabsManager.getTabsElement().triggerTabChange()}),view.addEventListener("viewdestroy",function(e){tabControllers.forEach(function(t){t.destroy&&t.destroy()})})}});
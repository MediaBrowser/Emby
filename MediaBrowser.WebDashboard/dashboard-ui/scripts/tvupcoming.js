define(["loading","datetime","libraryBrowser","cardBuilder","apphost","imageLoader","scrollStyles","emby-itemscontainer"],function(loading,datetime,libraryBrowser,cardBuilder,appHost,imageLoader){"use strict";function getUpcomingPromise(context,params){loading.show();var query={Limit:40,Fields:"AirTime,UserData",UserId:Dashboard.getCurrentUserId(),ImageTypeLimit:1,EnableImageTypes:"Primary,Backdrop,Banner,Thumb",EnableTotalRecordCount:!1};return query.ParentId=params.topParentId,ApiClient.getJSON(ApiClient.getUrl("Shows/Upcoming",query))}function loadUpcoming(context,params,promise){promise.then(function(result){var items=result.Items;items.length?context.querySelector(".noItemsMessage").style.display="none":context.querySelector(".noItemsMessage").style.display="block";var elem=context.querySelector("#upcomingItems");renderUpcoming(elem,items),loading.hide()})}function enableScrollX(){return browserInfo.mobile}function getThumbShape(){return enableScrollX()?"overflowBackdrop":"backdrop"}function renderUpcoming(elem,items){var i,length,groups=[],currentGroupName="",currentGroup=[];for(i=0,length=items.length;i<length;i++){var item=items[i],dateText="";if(item.PremiereDate)try{var premiereDate=datetime.parseISO8601Date(item.PremiereDate,!0);dateText=datetime.isRelativeDay(premiereDate,-1)?Globalize.translate("Yesterday"):libraryBrowser.getFutureDateText(premiereDate,!0)}catch(err){}dateText!=currentGroupName?(currentGroup.length&&groups.push({name:currentGroupName,items:currentGroup}),currentGroupName=dateText,currentGroup=[item]):currentGroup.push(item)}var html="";for(i=0,length=groups.length;i<length;i++){var group=groups[i];html+='<div class="homePageSection">',html+='<h1 class="listHeader">'+group.name+"</h1>";var allowBottomPadding=!0;enableScrollX()?(allowBottomPadding=!1,html+='<div is="emby-itemscontainer" class="itemsContainer hiddenScrollX">'):html+='<div is="emby-itemscontainer" class="itemsContainer vertical-wrap">';var supportsImageAnalysis=appHost.supports("imageanalysis");html+=cardBuilder.getCardsHtml({items:group.items,showLocationTypeIndicator:!1,shape:getThumbShape(),showTitle:!0,preferThumb:!0,lazy:!0,showDetailsMenu:!0,centerText:!supportsImageAnalysis,showParentTitle:!0,overlayText:!1,allowBottomPadding:allowBottomPadding,cardLayout:supportsImageAnalysis,vibrant:supportsImageAnalysis}),html+="</div>",html+="</div>"}elem.innerHTML=html,imageLoader.lazyChildren(elem)}return function(view,params,tabContent){var upcomingPromise,self=this;self.preRender=function(){upcomingPromise=getUpcomingPromise(view,params)},self.renderTab=function(){loadUpcoming(tabContent,params,upcomingPromise)}}});
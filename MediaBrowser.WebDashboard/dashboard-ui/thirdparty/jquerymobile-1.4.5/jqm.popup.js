define(["jqmwidget"],function(){!function($,undefined){var props={animation:{},transition:{}},testElement=document.createElement("a"),vendorPrefixes=["","webkit-","moz-","o-"];$.each(["animation","transition"],function(i,test){var testName=0===i?test+"-name":test;$.each(vendorPrefixes,function(j,prefix){if(testElement.style[$.camelCase(prefix+testName)]!==undefined)return props[test].prefix=prefix,!1}),props[test].duration=$.camelCase(props[test].prefix+test+"-duration"),props[test].event=$.camelCase(props[test].prefix+test+"-end"),""===props[test].prefix&&(props[test].event=props[test].event.toLowerCase())}),$(testElement).remove(),$.fn.animationComplete=function(callback,type,fallbackTime){var timer,duration,that=this,eventBinding=function(){clearTimeout(timer),callback.apply(this,arguments)},animationType=type&&"animation"!==type?"transition":"animation";return fallbackTime===undefined&&($(this).context!==document&&(duration=3e3*parseFloat($(this).css(props[animationType].duration))),(0===duration||duration===undefined||isNaN(duration))&&(duration=$.fn.animationComplete.defaultDuration)),timer=setTimeout(function(){$(that).off(props[animationType].event,eventBinding),callback.apply(that)},duration),$(this).one(props[animationType].event,eventBinding)},$.fn.animationComplete.defaultDuration=1e3}(jQuery),function($,undefined){function fitSegmentInsideSegment(windowSize,segmentSize,offset,desired){var returnValue=desired;return returnValue=windowSize<segmentSize?offset+(windowSize-segmentSize)/2:Math.min(Math.max(offset,desired-segmentSize/2),offset+windowSize-segmentSize)}function getWindowCoordinates(theWindow){return{x:theWindow.scrollLeft(),y:theWindow.scrollTop(),cx:theWindow[0].innerWidth||theWindow.width(),cy:theWindow[0].innerHeight||theWindow.height()}}$.extend($.mobile,{browser:{}}),$.mobile.browser.oldIE=function(){var v=3,div=document.createElement("div"),a=div.all||[];do div.innerHTML="<!--[if gt IE "+ ++v+"]><br><![endif]-->";while(a[0]);return v>4?v:!v}(),$.widget("mobile.popup",{options:{wrapperClass:null,theme:null,overlayTheme:null,shadow:!0,corners:!0,transition:"none",positionTo:"origin",tolerance:null,closeLinkSelector:"a[data-rel='back']",closeLinkEvents:"click.popup",navigateEvents:"navigate.popup",closeEvents:"navigate.popup pagebeforechange.popup",dismissible:!0,enhanced:!1,history:!$.mobile.browser.oldIE},_handleDocumentVmousedown:function(theEvent){this._isOpen&&$.contains(this._ui.container[0],theEvent.target)&&this._ignoreResizeEvents()},_create:function(){var theElement=this.element,myId=theElement.attr("id"),currentOptions=this.options;currentOptions.history=currentOptions.history&&$.mobile.ajaxEnabled&&$.mobile.hashListeningEnabled,this._on(this.document,{mousedown:"_handleDocumentVmousedown"}),$.extend(this,{_scrollTop:0,_page:theElement.parents("div[data-role='page']"),_ui:null,_fallbackTransition:"",_currentTransition:!1,_prerequisites:null,_isOpen:!1,_tolerance:null,_resizeData:null,_ignoreResizeTo:0,_orientationchangeInProgress:!1}),0===this._page.length&&(this._page=$("body")),currentOptions.enhanced?this._ui={container:theElement.parent(),screen:theElement.parent().prev(),placeholder:$(this.document[0].getElementById(myId+"-placeholder"))}:(this._ui=this._enhance(theElement,myId),this._applyTransition(currentOptions.transition)),this._setTolerance(currentOptions.tolerance)._ui.focusElement=this._ui.container,this._on(this._ui.screen,{click:"_eatEventAndClose"}),this._on(this.window,{orientationchange:$.proxy(this,"_handleWindowOrientationchange"),resize:$.proxy(this,"_handleWindowResize")}),this._on(this.document,{focusin:"_handleDocumentFocusIn"})},_delay:function(handler,delay){function handlerProxy(){return("string"==typeof handler?instance[handler]:handler).apply(instance,arguments)}var instance=this;return setTimeout(handlerProxy,delay||0)},_enhance:function(theElement,myId){var currentOptions=this.options,wrapperClass=currentOptions.wrapperClass,ui={screen:$("<div class='ui-screen-hidden ui-popup-screen "+this._themeClassFromOption("ui-overlay-",currentOptions.overlayTheme)+"'></div>"),placeholder:$("<div style='display: none;'><!-- placeholder --></div>"),container:$("<div class='ui-popup-container ui-popup-hidden ui-popup-truncate"+(wrapperClass?" "+wrapperClass:"")+"'></div>")},fragment=this.document[0].createDocumentFragment();return fragment.appendChild(ui.screen[0]),fragment.appendChild(ui.container[0]),myId&&(ui.screen.attr("id",myId+"-screen"),ui.container.attr("id",myId+"-popup"),ui.placeholder.attr("id",myId+"-placeholder").html("<!-- placeholder for "+myId+" -->")),this._page[0].appendChild(fragment),ui.placeholder.insertAfter(theElement),theElement.detach().addClass("ui-popup "+this._themeClassFromOption("ui-body-",currentOptions.theme)+" "+(currentOptions.shadow?"ui-overlay-shadow ":"")+(currentOptions.corners?"ui-corner-all ":"")).appendTo(ui.container),ui},_eatEventAndClose:function(theEvent){return theEvent.preventDefault(),theEvent.stopImmediatePropagation(),this.options.dismissible&&this.close(),!1},_resizeScreen:function(){var screen=this._ui.screen,popupHeight=this._ui.container.outerHeight(!0),screenHeight=screen.removeAttr("style").height(),documentHeight=this.document.height()-1;screenHeight<documentHeight?screen.height(documentHeight):popupHeight>screenHeight&&screen.height(popupHeight)},_expectResizeEvent:function(){var windowCoordinates=getWindowCoordinates(this.window);if(this._resizeData){if(windowCoordinates.x===this._resizeData.windowCoordinates.x&&windowCoordinates.y===this._resizeData.windowCoordinates.y&&windowCoordinates.cx===this._resizeData.windowCoordinates.cx&&windowCoordinates.cy===this._resizeData.windowCoordinates.cy)return!1;clearTimeout(this._resizeData.timeoutId)}return this._resizeData={timeoutId:this._delay("_resizeTimeout",200),windowCoordinates:windowCoordinates},!0},_resizeTimeout:function(){this._isOpen?this._expectResizeEvent()||(this._ui.container.hasClass("ui-popup-hidden")&&(this._ui.container.removeClass("ui-popup-hidden ui-popup-truncate"),this.reposition({positionTo:"window"}),this._ignoreResizeEvents()),this._resizeScreen(),this._resizeData=null,this._orientationchangeInProgress=!1):(this._resizeData=null,this._orientationchangeInProgress=!1)},_stopIgnoringResizeEvents:function(){this._ignoreResizeTo=0},_ignoreResizeEvents:function(){this._ignoreResizeTo&&clearTimeout(this._ignoreResizeTo),this._ignoreResizeTo=this._delay("_stopIgnoringResizeEvents",1e3)},_handleWindowResize:function(){this._isOpen&&0===this._ignoreResizeTo&&(!this._expectResizeEvent()&&!this._orientationchangeInProgress||this._ui.container.hasClass("ui-popup-hidden")||this._ui.container.addClass("ui-popup-hidden ui-popup-truncate").removeAttr("style"))},_handleWindowOrientationchange:function(){!this._orientationchangeInProgress&&this._isOpen&&0===this._ignoreResizeTo&&(this._expectResizeEvent(),this._orientationchangeInProgress=!0)},_handleDocumentFocusIn:function(theEvent){var target,targetElement=theEvent.target,ui=this._ui;if(this._isOpen){if(targetElement!==ui.container[0]){if(target=$(targetElement),!$.contains(ui.container[0],targetElement))return $(this.document[0].activeElement).one("focus",$.proxy(function(){this._safelyBlur(targetElement)},this)),ui.focusElement.focus(),theEvent.preventDefault(),theEvent.stopImmediatePropagation(),!1;ui.focusElement[0]===ui.container[0]&&(ui.focusElement=target)}this._ignoreResizeEvents()}},_themeClassFromOption:function(prefix,value){return value?"none"===value?"":prefix+value:prefix+"inherit"},_applyTransition:function(value){return value&&(this._ui.container.removeClass(this._fallbackTransition),"none"!==value&&(this._fallbackTransition=$.mobile._maybeDegradeTransition(value),"none"===this._fallbackTransition&&(this._fallbackTransition=""),this._ui.container.addClass(this._fallbackTransition))),this},_setOptions:function(newOptions){var currentOptions=this.options,theElement=this.element,screen=this._ui.screen;return newOptions.wrapperClass!==undefined&&this._ui.container.removeClass(currentOptions.wrapperClass).addClass(newOptions.wrapperClass),newOptions.theme!==undefined&&theElement.removeClass(this._themeClassFromOption("ui-body-",currentOptions.theme)).addClass(this._themeClassFromOption("ui-body-",newOptions.theme)),newOptions.overlayTheme!==undefined&&(screen.removeClass(this._themeClassFromOption("ui-overlay-",currentOptions.overlayTheme)).addClass(this._themeClassFromOption("ui-overlay-",newOptions.overlayTheme)),this._isOpen&&screen.addClass("in")),newOptions.shadow!==undefined&&theElement.toggleClass("ui-overlay-shadow",newOptions.shadow),newOptions.corners!==undefined&&theElement.toggleClass("ui-corner-all",newOptions.corners),newOptions.transition!==undefined&&(this._currentTransition||this._applyTransition(newOptions.transition)),newOptions.tolerance!==undefined&&this._setTolerance(newOptions.tolerance),newOptions.disabled!==undefined&&newOptions.disabled&&this.close(),this._super(newOptions)},_setTolerance:function(value){var ar,tol={t:30,r:15,b:30,l:15};if(value!==undefined)switch(ar=String(value).split(","),$.each(ar,function(idx,val){ar[idx]=parseInt(val,10)}),ar.length){case 1:isNaN(ar[0])||(tol.t=tol.r=tol.b=tol.l=ar[0]);break;case 2:isNaN(ar[0])||(tol.t=tol.b=ar[0]),isNaN(ar[1])||(tol.l=tol.r=ar[1]);break;case 4:isNaN(ar[0])||(tol.t=ar[0]),isNaN(ar[1])||(tol.r=ar[1]),isNaN(ar[2])||(tol.b=ar[2]),isNaN(ar[3])||(tol.l=ar[3])}return this._tolerance=tol,this},_clampPopupWidth:function(infoOnly){var menuSize,windowCoordinates=getWindowCoordinates(this.window),rectangle={x:this._tolerance.l,y:windowCoordinates.y+this._tolerance.t,cx:windowCoordinates.cx-this._tolerance.l-this._tolerance.r,cy:windowCoordinates.cy-this._tolerance.t-this._tolerance.b};return infoOnly||this._ui.container.css("max-width",rectangle.cx),menuSize={cx:this._ui.container.outerWidth(!0),cy:this._ui.container.outerHeight(!0)},{rc:rectangle,menuSize:menuSize}},_calculateFinalLocation:function(desired,clampInfo){var returnValue,rectangle=clampInfo.rc,menuSize=clampInfo.menuSize;return returnValue={left:fitSegmentInsideSegment(rectangle.cx,menuSize.cx,rectangle.x,desired.x),top:fitSegmentInsideSegment(rectangle.cy,menuSize.cy,rectangle.y,desired.y)},returnValue.top=Math.max(0,returnValue.top),returnValue.top-=Math.min(returnValue.top,Math.max(0,returnValue.top+menuSize.cy-this.document.height())),returnValue},_placementCoords:function(desired){return this._calculateFinalLocation(desired,this._clampPopupWidth())},_createPrerequisites:function(screenPrerequisite,containerPrerequisite,whenDone){var prerequisites,self=this;prerequisites={screen:$.Deferred(),container:$.Deferred()},prerequisites.screen.then(function(){prerequisites===self._prerequisites&&screenPrerequisite()}),prerequisites.container.then(function(){prerequisites===self._prerequisites&&containerPrerequisite()}),Promise.all([prerequisites.screen,prerequisites.container]).then(function(){prerequisites===self._prerequisites&&(self._prerequisites=null,whenDone())}),self._prerequisites=prerequisites},_animate:function(args){return this._ui.screen.removeClass(args.classToRemove).addClass(args.screenClassToAdd),args.prerequisites.screen.resolve(),args.transition&&"none"!==args.transition&&(args.applyTransition&&this._applyTransition(args.transition),this._fallbackTransition)?void this._ui.container.addClass(args.containerClassToAdd).removeClass(args.classToRemove).animationComplete($.proxy(args.prerequisites.container,"resolve")):(this._ui.container.removeClass(args.classToRemove),void args.prerequisites.container.resolve())},_desiredCoords:function(openOptions){var offset,dst=null,windowCoordinates=getWindowCoordinates(this.window),x=openOptions.x,y=openOptions.y,pTo=openOptions.positionTo;if(pTo&&"origin"!==pTo)if("window"===pTo)x=windowCoordinates.cx/2+windowCoordinates.x,y=windowCoordinates.cy/2+windowCoordinates.y;else{try{dst=$(pTo)}catch(err){dst=null}dst&&(dst.filter(":visible"),0===dst.length&&(dst=null))}return dst&&(offset=dst.offset(),x=offset.left+dst.outerWidth()/2,y=offset.top+dst.outerHeight()/2),("number"!==$.type(x)||isNaN(x))&&(x=windowCoordinates.cx/2+windowCoordinates.x),("number"!==$.type(y)||isNaN(y))&&(y=windowCoordinates.cy/2+windowCoordinates.y),{x:x,y:y}},_reposition:function(openOptions){openOptions={x:openOptions.x,y:openOptions.y,positionTo:openOptions.positionTo},this._trigger("beforeposition",undefined,openOptions),this._ui.container.offset(this._placementCoords(this._desiredCoords(openOptions)))},reposition:function(openOptions){this._isOpen&&this._reposition(openOptions)},_safelyBlur:function(currentElement){currentElement!==this.window[0]&&"body"!==currentElement.nodeName.toLowerCase()&&$(currentElement).blur()},_openPrerequisitesComplete:function(){var id=this.element.attr("id");this._ui.container.addClass("ui-popup-active"),this._isOpen=!0,this._resizeScreen(),$.contains(this._ui.container[0],this.document[0].activeElement)||this._safelyBlur(this.document[0].activeElement),this._ignoreResizeEvents(),id&&this.document.find("[aria-haspopup='true'][aria-owns='"+id+"']").attr("aria-expanded",!0),this._trigger("afteropen")},_open:function(options){var openOptions=$.extend({},this.options,options),androidBlacklist=function(){var ua=navigator.userAgent,wkmatch=ua.match(/AppleWebKit\/([0-9\.]+)/),wkversion=!!wkmatch&&wkmatch[1],androidmatch=ua.match(/Android (\d+(?:\.\d+))/),andversion=!!androidmatch&&androidmatch[1],chromematch=ua.indexOf("Chrome")>-1;return!(!(null!==androidmatch&&"4.0"===andversion&&wkversion&&wkversion>534.13)||chromematch)}();this._createPrerequisites($.noop,$.noop,$.proxy(this,"_openPrerequisitesComplete")),this._currentTransition=openOptions.transition,this._applyTransition(openOptions.transition),this._ui.screen.removeClass("ui-screen-hidden"),this._ui.container.removeClass("ui-popup-truncate"),this._reposition(openOptions),this._ui.container.removeClass("ui-popup-hidden"),this.options.overlayTheme&&androidBlacklist&&this.element.closest(".ui-page").addClass("ui-popup-open"),this._animate({additionalCondition:!0,transition:openOptions.transition,classToRemove:"",screenClassToAdd:"in",containerClassToAdd:"in",applyTransition:!1,prerequisites:this._prerequisites})},_closePrerequisiteScreen:function(){this._ui.screen.removeClass("out").addClass("ui-screen-hidden")},_closePrerequisiteContainer:function(){this._ui.container.removeClass("reverse out").addClass("ui-popup-hidden ui-popup-truncate").removeAttr("style")},_closePrerequisitesDone:function(){var container=this._ui.container,id=this.element.attr("id");$.mobile.popup.active=undefined,$(":focus",container[0]).add(container[0]).blur(),id&&this.document.find("[aria-haspopup='true'][aria-owns='"+id+"']").attr("aria-expanded",!1),this._trigger("afterclose")},_close:function(immediate){this._ui.container.removeClass("ui-popup-active"),this._page.removeClass("ui-popup-open"),this._isOpen=!1,this._createPrerequisites($.proxy(this,"_closePrerequisiteScreen"),$.proxy(this,"_closePrerequisiteContainer"),$.proxy(this,"_closePrerequisitesDone")),this._animate({additionalCondition:this._ui.screen.hasClass("in"),transition:immediate?"none":this._currentTransition,classToRemove:"in",screenClassToAdd:"out",containerClassToAdd:"reverse out",applyTransition:!0,prerequisites:this._prerequisites})},_unenhance:function(){this.options.enhanced||(this._setOptions({theme:$.mobile.popup.prototype.options.theme}),this.element.detach().insertAfter(this._ui.placeholder).removeClass("ui-popup ui-overlay-shadow ui-corner-all ui-body-inherit"),this._ui.screen.remove(),this._ui.container.remove(),this._ui.placeholder.remove())},_destroy:function(){return $.mobile.popup.active===this?(this.element.one("popupafterclose",$.proxy(this,"_unenhance")),this.close()):this._unenhance(),this},_closePopup:function(theEvent,data){var parsedDst,toUrl,currentOptions=this.options,immediate=!1;theEvent&&theEvent.isDefaultPrevented()||$.mobile.popup.active!==this||(window.scrollTo(0,this._scrollTop),theEvent&&"pagebeforechange"===theEvent.type&&data&&(parsedDst="string"==typeof data.toPage?data.toPage:data.toPage.data("url"),parsedDst=$.mobile.path.parseUrl(parsedDst),toUrl=parsedDst.pathname+parsedDst.search+parsedDst.hash,this._myUrl!==$.mobile.path.makeUrlAbsolute(toUrl)?immediate=!0:theEvent.preventDefault()),this.window.off(currentOptions.closeEvents),this.element.off(currentOptions.closeLinkEvents,currentOptions.closeLinkSelector),this._close(immediate))},_bindContainerClose:function(){this.window.on(this.options.closeEvents,$.proxy(this,"_closePopup"))},widget:function(){return this._ui.container},open:function(options){var url,hashkey,activePage,currentIsDialog,hasHash,urlHistory,self=this,currentOptions=this.options;return $.mobile.popup.active||currentOptions.disabled?this:($.mobile.popup.active=this,this._scrollTop=this.window.scrollTop(),currentOptions.history?(urlHistory=$.mobile.navigate.history,hashkey=$.mobile.dialogHashKey,activePage=$.mobile.activePage,currentIsDialog=!!activePage&&activePage.hasClass("ui-dialog"),this._myUrl=url=urlHistory.getActive().url,(hasHash=url.indexOf(hashkey)>-1&&!currentIsDialog&&urlHistory.activeIndex>0)?(self._open(options),self._bindContainerClose(),this):(url.indexOf(hashkey)!==-1||currentIsDialog?url=$.mobile.path.parseLocation().hash+hashkey:url+=url.indexOf("#")>-1?hashkey:"#"+hashkey,this.window.one("beforenavigate",function(theEvent){theEvent.preventDefault(),self._open(options),self._bindContainerClose()}),this.urlAltered=!0,$.mobile.navigate(url,{role:"dialog"}),this)):(self._open(options),self._bindContainerClose(),self.element.on(currentOptions.closeLinkEvents,currentOptions.closeLinkSelector,function(theEvent){self.close(),theEvent.preventDefault()}),this))},close:function(){return $.mobile.popup.active!==this?this:(this._scrollTop=this.window.scrollTop(),this.options.history&&this.urlAltered?($.mobile.pageContainer.pagecontainer("back"),this.urlAltered=!1):this._closePopup(),this)}}),$.mobile.popup.handleLink=function($link){var offset,path=$.mobile.path,popup=$(path.hashToSelector(path.parseUrl($link.attr("href")).hash)).first();popup.length>0&&popup.data("mobile-popup")&&(offset=$link.offset(),popup.popup("open",{x:offset.left+$link.outerWidth()/2,y:offset.top+$link.outerHeight()/2,transition:$link.data("transition"),positionTo:$link.data("position-to")})),setTimeout(function(){$link.removeClass($.mobile.activeBtnClass)},300)},$(document).on("pagebeforechange",function(theEvent,data){"popup"===data.options.role&&($.mobile.popup.handleLink(data.options.link),theEvent.preventDefault())})}(jQuery),function($,undefined){function getArrow(){var clone=uiTemplate.clone(),gd=clone.eq(0),ct=clone.eq(1),ar=ct.children();return{arEls:ct.add(gd),gd:gd,ct:ct,ar:ar}}var ieHack=$.mobile.browser.oldIE&&$.mobile.browser.oldIE<=8,uiTemplate=$("<div class='ui-popup-arrow-guide'></div><div class='ui-popup-arrow-container"+(ieHack?" ie":"")+"'><div class='ui-popup-arrow'></div></div>");$.widget("mobile.popup",$.mobile.popup,{options:{arrow:""},_create:function(){var ar,ret=this._super();return this.options.arrow&&(this._ui.arrow=ar=this._addArrow()),ret},_addArrow:function(){var theme,opts=this.options,ar=getArrow();return theme=this._themeClassFromOption("ui-body-",opts.theme),ar.ar.addClass(theme+(opts.shadow?" ui-overlay-shadow":"")),ar.arEls.hide().appendTo(this.element),ar},_unenhance:function(){var ar=this._ui.arrow;return ar&&ar.arEls.remove(),this._super()},_tryAnArrow:function(p,dir,desired,s,best){var result,r,diff,desiredForArrow={},tip={};return s.arFull[p.dimKey]>s.guideDims[p.dimKey]?best:(desiredForArrow[p.fst]=desired[p.fst]+(s.arHalf[p.oDimKey]+s.menuHalf[p.oDimKey])*p.offsetFactor-s.contentBox[p.fst]+(s.clampInfo.menuSize[p.oDimKey]-s.contentBox[p.oDimKey])*p.arrowOffsetFactor,desiredForArrow[p.snd]=desired[p.snd],result=s.result||this._calculateFinalLocation(desiredForArrow,s.clampInfo),r={x:result.left,y:result.top},tip[p.fst]=r[p.fst]+s.contentBox[p.fst]+p.tipOffset,tip[p.snd]=Math.max(result[p.prop]+s.guideOffset[p.prop]+s.arHalf[p.dimKey],Math.min(result[p.prop]+s.guideOffset[p.prop]+s.guideDims[p.dimKey]-s.arHalf[p.dimKey],desired[p.snd])),diff=Math.abs(desired.x-tip.x)+Math.abs(desired.y-tip.y),(!best||diff<best.diff)&&(tip[p.snd]-=s.arHalf[p.dimKey]+result[p.prop]+s.contentBox[p.snd],best={dir:dir,diff:diff,result:result,posProp:p.prop,posVal:tip[p.snd]}),best)},_getPlacementState:function(clamp){var offset,gdOffset,ar=this._ui.arrow,state={clampInfo:this._clampPopupWidth(!clamp),arFull:{cx:ar.ct.width(),cy:ar.ct.height()},guideDims:{cx:ar.gd.width(),cy:ar.gd.height()},guideOffset:ar.gd.offset()};return offset=this.element.offset(),ar.gd.css({left:0,top:0,right:0,bottom:0}),gdOffset=ar.gd.offset(),state.contentBox={x:gdOffset.left-offset.left,y:gdOffset.top-offset.top,cx:ar.gd.width(),cy:ar.gd.height()},ar.gd.removeAttr("style"),state.guideOffset={left:state.guideOffset.left-offset.left,top:state.guideOffset.top-offset.top},state.arHalf={cx:state.arFull.cx/2,cy:state.arFull.cy/2},state.menuHalf={cx:state.clampInfo.menuSize.cx/2,cy:state.clampInfo.menuSize.cy/2},state},_placementCoords:function(desired){var state,best,params,elOffset,bgRef,optionValue=this.options.arrow,ar=this._ui.arrow;return ar?(ar.arEls.show(),bgRef={},state=this._getPlacementState(!0),params={l:{fst:"x",snd:"y",prop:"top",dimKey:"cy",oDimKey:"cx",offsetFactor:1,tipOffset:-state.arHalf.cx,arrowOffsetFactor:0},r:{fst:"x",snd:"y",prop:"top",dimKey:"cy",oDimKey:"cx",offsetFactor:-1,tipOffset:state.arHalf.cx+state.contentBox.cx,arrowOffsetFactor:1},b:{fst:"y",snd:"x",prop:"left",dimKey:"cx",oDimKey:"cy",offsetFactor:-1,tipOffset:state.arHalf.cy+state.contentBox.cy,arrowOffsetFactor:1},t:{fst:"y",snd:"x",prop:"left",dimKey:"cx",oDimKey:"cy",offsetFactor:1,tipOffset:-state.arHalf.cy,arrowOffsetFactor:0}},$.each((optionValue===!0?"l,t,r,b":optionValue).split(","),$.proxy(function(key,value){best=this._tryAnArrow(params[value],value,desired,state,best)},this)),best?(ar.ct.removeClass("ui-popup-arrow-l ui-popup-arrow-t ui-popup-arrow-r ui-popup-arrow-b").addClass("ui-popup-arrow-"+best.dir).removeAttr("style").css(best.posProp,best.posVal).show(),ieHack||(elOffset=this.element.offset(),bgRef[params[best.dir].fst]=ar.ct.offset(),bgRef[params[best.dir].snd]={left:elOffset.left+state.contentBox.x,top:elOffset.top+state.contentBox.y}),best.result):(ar.arEls.hide(),this._super(desired))):this._super(desired)},_setOptions:function(opts){var newTheme,oldTheme=this.options.theme,ar=this._ui.arrow,ret=this._super(opts);if(opts.arrow!==undefined){if(!ar&&opts.arrow)return void(this._ui.arrow=this._addArrow());ar&&!opts.arrow&&(ar.arEls.remove(),this._ui.arrow=null)}return ar=this._ui.arrow,ar&&(opts.theme!==undefined&&(oldTheme=this._themeClassFromOption("ui-body-",oldTheme),newTheme=this._themeClassFromOption("ui-body-",opts.theme),ar.ar.removeClass(oldTheme).addClass(newTheme)),opts.shadow!==undefined&&ar.ar.toggleClass("ui-overlay-shadow",opts.shadow)),ret},_destroy:function(){var ar=this._ui.arrow;return ar&&ar.arEls.remove(),this._super()}})}(jQuery)});
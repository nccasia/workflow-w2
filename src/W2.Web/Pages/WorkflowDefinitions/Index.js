(function ($) {

    var localize = function (key) {
        return abp.localization.getResource('AbpUi')(key);
    };

    (function () {
        if (!$.fn.dataTableExt) {
            return;
        }

        var getVisibilityValue = function (visibilityField, record, tableInstance) {
            if (visibilityField === undefined) {
                return true;
            }

            if (abp.utils.isFunction(visibilityField)) {
                return visibilityField(record, tableInstance);
            } else {
                return visibilityField;
            }
        };

        var htmlEncode = function (html) {
            return $('<div/>').text(html).html();
        }

        var _createDropdownItem = function (record, fieldItem, tableInstance) {
            var $li = $('<li/>');
            var $a = $('<a/>');

            if (fieldItem.displayNameHtml) {
                $a.html(fieldItem.text);
            } else {

                if (fieldItem.icon !== undefined && fieldItem.icon) {
                    $a.append($("<i>").addClass("fa fa-" + fieldItem.icon + " me-1"));
                } else if (fieldItem.iconClass) {
                    $a.append($("<i>").addClass(fieldItem.iconClass + " me-1"));
                }

                $a.append(htmlEncode(fieldItem.text));
            }

            if (fieldItem.action) {
                $a.click(function (e) {
                    e.preventDefault();

                    if (!$(this).closest('li').hasClass('disabled')) {
                        if (fieldItem.confirmMessage) {
                            abp.message.confirm(fieldItem.confirmMessage({ record: record, table: tableInstance }))
                                .done(function (accepted) {
                                    if (accepted) {
                                        fieldItem.action({ record: record, table: tableInstance });
                                    }
                                });
                        } else {
                            fieldItem.action({ record: record, table: tableInstance });
                        }
                    }
                });
            }

            $a.appendTo($li);
            return $li;
        };

        var _createButtonDropdown = function (record, field, tableInstance) {
            if (field.items.length === 1) {
                var firstItem = field.items[0];
                if (!getVisibilityValue(firstItem.visible, record, tableInstance)) {
                    return "";
                }

                var $button = $('<button type="button" class="btn btn-sm btn-primary abp-action-button" style="margin-right: 5px;"></button>');

                if (firstItem.displayNameHtml) {
                    $button.html(firstItem.text);
                } else {
                    if (firstItem.icon !== undefined && firstItem.icon) {
                        $button.append($("<i>").addClass("fa fa-" + firstItem.icon + " me-1"));
                    } else if (firstItem.iconClass) {
                        $button.append($("<i>").addClass(firstItem.iconClass + " me-1"));
                    }
                    $button.append(htmlEncode(firstItem.text));
                }

                if (firstItem.enabled && !firstItem.enabled({ record: record, table: tableInstance })) {
                    $button.addClass('disabled');
                }

                if (firstItem.action) {
                    $button.click(function (e) {
                        e.preventDefault();

                        if (!$(this).hasClass('disabled')) {
                            if (firstItem.confirmMessage) {
                                abp.message.confirm(firstItem.confirmMessage({ record: record, table: tableInstance }))
                                    .done(function (accepted) {
                                        if (accepted) {
                                            firstItem.action({ record: record, table: tableInstance });
                                        }
                                    });
                            } else {
                                firstItem.action({ record: record, table: tableInstance });
                            }
                        }
                    });
                }

                return $button;
            }
            var $container = $('<div/>')
                .addClass('dropdown')
                .addClass('abp-action-button')
                .css("margin-right", "5px");
            var $dropdownButton = $('<button/>');

            if (field.icon !== undefined && field.icon) {
                $dropdownButton.append($("<i>").addClass("fa fa-" + field.icon + " me-1"));
            } else if (field.iconClass) {
                $dropdownButton.append($("<i>").addClass(field.iconClass + " me-1"));
            } else {
                $dropdownButton.append($("<i>").addClass("fa fa-cog me-1"));
            }

            if (field.text) {
                $dropdownButton.append(htmlEncode(field.text));
            } else {
                $dropdownButton.append(htmlEncode(localize("DatatableActionDropdownDefaultText")));
            }

            $dropdownButton
                .addClass('btn btn-primary btn-sm dropdown-toggle')
                .attr('data-bs-toggle', 'dropdown')
                .attr('aria-haspopup', 'true')
                .attr('aria-expanded', 'false');

            if (field.cssClass) {
                $dropdownButton.addClass(field.cssClass);
            }

            var $dropdownItemsContainer = $('<ul/>').addClass('dropdown-menu');

            for (var i = 0; i < field.items.length; i++) {
                var fieldItem = field.items[i];

                var isVisible = getVisibilityValue(fieldItem.visible, record, tableInstance);
                if (!isVisible) {
                    continue;
                }

                var $dropdownItem = _createDropdownItem(record, fieldItem, tableInstance);

                if (fieldItem.enabled && !fieldItem.enabled({ record: record, table: tableInstance })) {
                    $dropdownItem.addClass('disabled');
                }

                $dropdownItem.appendTo($dropdownItemsContainer);
            }

            if ($dropdownItemsContainer.find('li').length > 0) {
                $dropdownItemsContainer.appendTo($container);
            } else {
                $dropdownButton.attr('disabled', 'disabled');
            }
            $dropdownButton.prependTo($container);
            return $container;
        };

        var _createMultiButtonDropdown = function (record, field, tableInstance) {
            var $template = $('<div/>')
                .addClass('d-flex')
                .addClass('justify-content-center');

            for (let items of field.items) {
                $template.append(_createButtonDropdown(record, {items}, tableInstance));
            }

            return $template;
        };

        var _createSingleButton = function (record, field, tableInstance) {
            $(field.element).data(record);

            var isVisible = getVisibilityValue(field.visible, record, tableInstance);

            if (isVisible) {
                return field.element;
            }

            return "";
        };

        var _createRowAction = function (record, field, tableInstance) {
            if (field.items && field.items.length > 0) {
                if (!field.multiple)
                    return _createButtonDropdown(record, field, tableInstance);
                else
                    return _createMultiButtonDropdown(record, field, tableInstance);
            } else if (field.element) {
                var $singleActionButton = _createSingleButton(record, field, tableInstance);
                if ($singleActionButton === "") {
                    return "";
                }

                return $singleActionButton.clone(true);
            }

            throw "DTE#1: Cannot create row action. Either set element or items fields!";
        };

        var hideColumnWithoutRedraw = function (tableInstance, colIndex) {
            tableInstance.fnSetColumnVis(colIndex, false, false);
        };

        var hideEmptyColumn = function (cellContent, tableInstance, colIndex) {
            if (cellContent == "") {
                hideColumnWithoutRedraw(tableInstance, colIndex);
            }
        };

        var renderRowActions = function (tableInstance, nRow, aData, iDisplayIndex, iDisplayIndexFull) {
            var columns;

            if (tableInstance.aoColumns) {
                columns = tableInstance.aoColumns;
            } else {
                columns = tableInstance.fnSettings().aoColumns;
            }

            if (!columns) {
                return;
            }

            var cells = $(nRow).children("td");

            for (var colIndex = 0; colIndex < columns.length; colIndex++) {
                var column = columns[colIndex];
                if (column.rowAction) {
                    var $actionContainer = _createRowAction(aData, column.rowAction, tableInstance);
                    hideEmptyColumn($actionContainer, tableInstance, colIndex);

                    if ($actionContainer) {
                        var $actionButton = $(cells[colIndex]).find(".abp-action-button");
                        if ($actionButton.length === 0) {
                            $(cells[colIndex]).empty().append($actionContainer);
                        }
                    }
                }
            }
        };

        $.fn.dataTableExt.oApi.renderRowActions =
            function (tableInstance, nRow, aData, iDisplayIndex, iDisplayIndexFull) {
                renderRowActions(tableInstance, nRow, aData, iDisplayIndex, iDisplayIndexFull);
            };

        if (!$.fn.dataTable) {
            return;
        }

        var _existingDefaultFnRowCallback = $.fn.dataTable.defaults.fnRowCallback;
        $.extend(true,
            $.fn.dataTable.defaults,
            {
                fnRowCallback: function (nRow, aData, iDisplayIndex, iDisplayIndexFull) {
                    renderRowActions(this, nRow, aData, iDisplayIndex, iDisplayIndexFull);
                }
            });

        //Delay for processing indicator
        var defaultDelayForProcessingIndicator = 500;
        var _existingDefaultFnPreDrawCallback = $.fn.dataTable.defaults.fnPreDrawCallback;
        $.extend(true,
            $.fn.dataTable.defaults,
            {
                fnPreDrawCallback: function (settings) {
                    if (_existingDefaultFnPreDrawCallback) {
                        _existingDefaultFnPreDrawCallback(settings);
                    }

                    var $tableWrapper = $(settings.nTableWrapper);
                    var $processing = $tableWrapper.find(".dataTables_processing");
                    var timeoutHandles = [];
                    var cancelHandles = [];

                    $tableWrapper.on('processing.dt',
                        function (e, settings, processing) {
                            if ((settings.oInit.processingDelay !== undefined && settings.oInit.processingDelay < 1) || defaultDelayForProcessingIndicator < 1) {
                                return;
                            }

                            if (processing) {
                                $processing.hide();

                                var delay = settings.oInit.processingDelay === undefined
                                    ? defaultDelayForProcessingIndicator
                                    : settings.oInit.processingDelay;

                                cancelHandles[settings.nTableWrapper.id] = false;

                                timeoutHandles[settings.nTableWrapper.id] = setTimeout(function () {
                                    if (cancelHandles[settings.nTableWrapper.id] === true) {
                                        return;
                                    }

                                    $processing.show();
                                }, delay);
                            }
                            else {
                                clearTimeout(timeoutHandles[settings.nTableWrapper.id]);
                                cancelHandles[settings.nTableWrapper.id] = true;
                                $processing.hide();
                            }
                        });
                }
            });

    })();

})(jQuery);

$(function () {
    var l = abp.localization.getResource('W2');

    var newWorkflowInstanceModal = new abp.ModalManager(abp.appPath + 'WorkflowDefinitions/NewWorkflowInstanceModal');

    var defineWorkflowInputModal = new abp.ModalManager({
        viewUrl: abp.appPath + 'WorkflowDefinitions/DefineWorkflowInputModal',
        scriptUrl: '/Pages/WorkflowDefinitions/DefineWorkflowInputModal.js',
        modalClass: 'DefineWorkflowInputModal'
    });


    var createWorkflowDefinitionModal = new abp.ModalManager(abp.appPath + 'WorkflowDefinitions/CreateModal');

    var dataTable = $('#WorkflowDefinitionsTable').DataTable(
        abp.libs.datatables.normalizeConfiguration({
            serverSide: true,
            searching: false,
            paging: false,
            scrollX: true,
            ordering: false,
            ajax: abp.libs.datatables.createAjax(w2.workflowDefinitions.workflowDefinition.listAll),
            columnDefs: buildColumn()
        })
    );

    function buildColumn() {
        const hasDesignWorkflowPermission = abp.auth.isGranted("WorkflowManagement.WorkflowDefinitions.Design")
        var items = []
        if (hasDesignWorkflowPermission)
            items.push({
                title: l('WorkflowDefinition:Name'),
                data: "name"
            })
        items.push({
            title: l('WorkflowDefinition:DisplayName'),
            data: "displayName",
        })
        if (hasDesignWorkflowPermission)
            items.push({
                title: l('WorkflowDefinition:Version'),
                data: "version"
            },
            {
                title: l('WorkflowDefinition:IsPublished'),
                data: "isPublished",
                })
        items.push({
            title: "",
            rowAction: {
                items: buildRowActionItems(),
                multiple: true
            }
        })
        return items
    }

    function buildRowActionItems() {
        const items = [];
        if (abp.auth.isGranted("WorkflowManagement.WorkflowInstances.Create")) {
            items.push([
                {
                    text: l('WorkflowDefinition:NewWorkflowInstance'),
                    action: function (data) {
                        newWorkflowInstanceModal.open({
                            workflowDefinitionId: data.record.definitionId,
                            propertiesDefinitionJson: data.record.inputDefinition
                                ? JSON.stringify(data.record.inputDefinition.propertyDefinitions)
                                : null
                        });
                    },
                    visible: abp.auth.isGranted("WorkflowManagement.WorkflowInstances.Create")
                }
            ]);
        }
        const hasDesignWorkflowPermission = abp.auth.isGranted("WorkflowManagement.WorkflowDefinitions.Design");
        if (hasDesignWorkflowPermission) {
            items.push(
                [
                    {
                        text: l('WorkflowDefinition:DefineInput'),
                        action: function (data) {
                            defineWorkflowInputModal.open({
                                workflowDefinitionId: data.record.definitionId
                            });
                        },
                        visible: hasDesignWorkflowPermission
                    },
                    {
                        text: l('WorkflowDefinition:OpenDesigner'),
                        action: function (data) {
                            window.location.href = abp.appPath + 'WorkflowDefinitions/Designer?workflowDefinitionId=' + data.record.definitionId;
                        },
                        visible: hasDesignWorkflowPermission
                    },
                    {
                        text: l('Delete'),
                        action: function (data) {
                            w2.workflowDefinitions.workflowDefinition
                                .delete(data.record.definitionId)
                                .then(function () {
                                    dataTable.ajax.reload();
                                    abp.notify.success(l('SuccessfullyDeleted'));
                                });
                        },
                        confirmMessage: function (data) {
                            return l(
                                'WorkflowDefinition:DeleteConfirmationMessage',
                                data.record.displayName
                            );
                        },
                        visible: hasDesignWorkflowPermission
                    }
                ]
            );
        }

        if (items[0].length == 1) {
            items[0][0].displayNameHtml = true;
            items[0][0].text = `<i class="fa fa-plus" title="${l('WorkflowDefinition:NewWorkflowInstance')}"></i>`;
        }
        return items;
    }

    defineWorkflowInputModal.onResult(function () {
        dataTable.ajax.reload();
    });

    defineWorkflowSettingModal.onResult(function () {
        dataTable.ajax.reload();
    })

    $("#CreateWorkflowDefinition").click(function (e) {
        e.preventDefault();
        createWorkflowDefinitionModal.open();
    });

    createWorkflowDefinitionModal.onResult(function () {
        if (arguments?.length > 1) {
            arguments[1].xhr.then(res => {
                window.open(abp.appPath + 'WorkflowDefinitions/Designer?workflowDefinitionId=' + res);
            });
        }

        dataTable.ajax.reload();
    });

    newWorkflowInstanceModal.onResult(function () {
        if (arguments?.length > 1) {
            arguments[1].xhr.then(res => {
                abp.notify.success('Making request successfully.');
                window.open(abp.appPath + 'WorkflowInstances/Designer?id=' + res);
            });
        }
    });
});
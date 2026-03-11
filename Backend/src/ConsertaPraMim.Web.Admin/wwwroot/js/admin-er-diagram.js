(function () {
    const host = document.getElementById('erDiagramApp');
    const payload = window.adminErDiagramData;
    if (!host || !payload || typeof React === 'undefined' || typeof ReactDOM === 'undefined' || typeof window.ReactFlow === 'undefined') {
        return;
    }

    const e = React.createElement;
    const { useEffect, useState } = React;
    const { ReactFlow, Background, Controls, MiniMap, Handle, Position, MarkerType } = window.ReactFlow;
    const allTables = Array.isArray(payload.tables) ? payload.tables : [];
    const allRelationships = Array.isArray(payload.relationships) ? payload.relationships : [];
    const ALL_DOMAIN = '__all__';
    const NODE_WIDTH = 320;
    const DOMAIN_COLORS = ['#0f4c81', '#1d4ed8', '#047857', '#b45309', '#7c3aed', '#be123c', '#0369a1', '#0f766e', '#4d7c0f', '#9333ea', '#c2410c', '#334155'];
    const canAutoLayout = Boolean(window.dagre && window.dagre.graphlib && typeof window.dagre.layout === 'function');

    const compareText = (left, right) => String(left || '').localeCompare(String(right || ''), 'pt-BR', { sensitivity: 'base' });
    const sameText = (left, right) => compareText(left, right) === 0;

    function hashText(value) {
        let hash = 0;
        const text = String(value || '');
        for (let index = 0; index < text.length; index++) {
            hash = ((hash << 5) - hash) + text.charCodeAt(index);
            hash |= 0;
        }

        return Math.abs(hash);
    }

    function getDomainColor(domainName) {
        return DOMAIN_COLORS[hashText(domainName) % DOMAIN_COLORS.length];
    }

    function estimateNodeHeight(table) {
        const columnCount = Array.isArray(table.columns) && table.columns.length > 0 ? table.columns.length : 1;
        return 66 + (columnCount * 34);
    }

    function buildRelationshipLabel(relationship) {
        const cardinality = relationship.isUnique ? '1:1' : '1:N';
        const dependentColumns = Array.isArray(relationship.dependentColumns) ? relationship.dependentColumns.filter(Boolean) : [];
        if (dependentColumns.length === 0) {
            return cardinality;
        }

        const previewColumns = dependentColumns.length <= 2
            ? dependentColumns.join(', ')
            : `${dependentColumns.slice(0, 2).join(', ')}, ...`;
        return `${cardinality} | ${previewColumns}`;
    }

    function selectColumnCount(tableCount) {
        if (tableCount >= 14) {
            return 3;
        }
        if (tableCount >= 6) {
            return 2;
        }

        return 1;
    }

    function createMasonryLayout(tables, startX, startY, columnCount) {
        const columnWidth = NODE_WIDTH;
        const horizontalGap = 52;
        const verticalGap = 42;
        const columnHeights = Array(columnCount).fill(startY);
        const positions = new Map();
        const orderedTables = [...tables].sort((left, right) => compareText(left.fullName, right.fullName));

        for (const table of orderedTables) {
            let targetColumn = 0;
            for (let index = 1; index < columnCount; index++) {
                if (columnHeights[index] < columnHeights[targetColumn]) {
                    targetColumn = index;
                }
            }

            positions.set(table.fullName, {
                x: startX + (targetColumn * (columnWidth + horizontalGap)),
                y: columnHeights[targetColumn]
            });

            columnHeights[targetColumn] += estimateNodeHeight(table) + verticalGap;
        }

        return {
            positions: positions,
            width: (columnCount * columnWidth) + ((columnCount - 1) * horizontalGap)
        };
    }

    function resolveAutoLayoutDirection(nodeCount, focused) {
        if (focused) {
            return 'TB';
        }

        return nodeCount >= 10 ? 'LR' : 'TB';
    }

    function createAutoLayoutGraph(graph, direction) {
        if (!canAutoLayout || graph.nodes.length === 0 || graph.edges.length === 0) {
            return graph;
        }

        const dagreGraph = new window.dagre.graphlib.Graph();
        dagreGraph.setDefaultEdgeLabel(function () { return {}; });
        dagreGraph.setGraph({
            rankdir: direction,
            ranksep: direction === 'TB' ? 118 : 168,
            nodesep: direction === 'TB' ? 72 : 54,
            edgesep: 28,
            marginx: 42,
            marginy: 42
        });

        for (const node of graph.nodes) {
            const nodeHeight = estimateNodeHeight({ columns: node.data ? node.data.columns : [] });
            dagreGraph.setNode(node.id, {
                width: NODE_WIDTH,
                height: nodeHeight
            });
        }

        for (const edge of graph.edges) {
            dagreGraph.setEdge(edge.source, edge.target);
        }

        window.dagre.layout(dagreGraph);

        const isHorizontal = direction === 'LR';
        return {
            nodes: graph.nodes.map(function (node) {
                const layoutNode = dagreGraph.node(node.id);
                const nodeHeight = estimateNodeHeight({ columns: node.data ? node.data.columns : [] });
                return {
                    ...node,
                    sourcePosition: isHorizontal ? Position.Right : Position.Bottom,
                    targetPosition: isHorizontal ? Position.Left : Position.Top,
                    position: {
                        x: layoutNode.x - (NODE_WIDTH / 2),
                        y: layoutNode.y - (nodeHeight / 2)
                    }
                };
            }),
            edges: graph.edges.map(function (edge) {
                return {
                    ...edge,
                    type: 'smoothstep'
                };
            })
        };
    }

    function createGraph(tables, relationships, positions, focusedTable) {
        const nodes = [...tables]
            .sort((left, right) => compareText(left.fullName, right.fullName))
            .map((table) => ({
                id: table.fullName,
                type: 'table',
                position: positions.get(table.fullName) || { x: 0, y: 0 },
                data: {
                    schema: table.schema,
                    name: table.name,
                    fullName: table.fullName,
                    domainName: table.domainName,
                    domainColor: getDomainColor(table.domainName),
                    columns: Array.isArray(table.columns) ? table.columns : [],
                    focused: sameText(table.fullName, focusedTable)
                }
            }));

        const edges = [...relationships]
            .sort((left, right) => compareText(left.principalTable, right.principalTable) || compareText(left.dependentTable, right.dependentTable))
            .map((relationship, index) => {
                const highlighted = focusedTable && (sameText(relationship.principalTable, focusedTable) || sameText(relationship.dependentTable, focusedTable));
                return {
                    id: `edge-${index}-${relationship.principalTable}-${relationship.dependentTable}`,
                    source: relationship.principalTable,
                    target: relationship.dependentTable,
                    type: 'smoothstep',
                    animated: Boolean(highlighted),
                    markerEnd: {
                        type: MarkerType.ArrowClosed,
                        width: 18,
                        height: 18,
                        color: highlighted ? '#0f172a' : '#64748b'
                    },
                    label: buildRelationshipLabel(relationship),
                    style: {
                        stroke: highlighted ? '#0f172a' : '#64748b',
                        strokeWidth: highlighted ? 2.2 : 1.7
                    },
                    labelStyle: {
                        fill: '#334155',
                        fontWeight: 700,
                        fontSize: 11
                    },
                    labelBgPadding: [8, 4],
                    labelBgBorderRadius: 999,
                    labelBgStyle: {
                        fill: '#ffffff',
                        fillOpacity: 0.96,
                        stroke: '#cbd5e1'
                    }
                };
            });

        return { nodes: nodes, edges: edges };
    }

    function buildOverviewGraph() {
        const groups = new Map();
        for (const table of allTables) {
            const key = table.domainName || 'Outros';
            if (!groups.has(key)) {
                groups.set(key, []);
            }

            groups.get(key).push(table);
        }

        const positions = new Map();
        let currentX = 48;
        for (const group of Array.from(groups.entries()).sort((left, right) => compareText(left[0], right[0]))) {
            const layout = createMasonryLayout(group[1], currentX, 92, selectColumnCount(group[1].length));
            for (const entry of layout.positions.entries()) {
                positions.set(entry[0], entry[1]);
            }

            currentX += layout.width + 110;
        }

        return createGraph(allTables, allRelationships, positions, '');
    }

    function buildDomainGraph(domainName) {
        const scopedTables = allTables.filter((table) => sameText(table.domainName, domainName));
        const tableKeys = new Set(scopedTables.map((table) => table.fullName));
        const scopedRelationships = allRelationships.filter((relationship) =>
            tableKeys.has(relationship.principalTable) &&
            tableKeys.has(relationship.dependentTable));
        const layout = createMasonryLayout(scopedTables, 56, 92, selectColumnCount(scopedTables.length));
        return createGraph(scopedTables, scopedRelationships, layout.positions, '');
    }

    function buildFocusGraph(tableFullName) {
        const selectedTable = allTables.find((table) => sameText(table.fullName, tableFullName));
        if (!selectedTable) {
            return buildOverviewGraph();
        }

        const principalRelationships = allRelationships.filter((relationship) => sameText(relationship.dependentTable, tableFullName));
        const dependentRelationships = allRelationships.filter((relationship) => sameText(relationship.principalTable, tableFullName));
        const principalTables = principalRelationships.map((relationship) => allTables.find((table) => sameText(table.fullName, relationship.principalTable))).filter(Boolean);
        const dependentTables = dependentRelationships.map((relationship) => allTables.find((table) => sameText(table.fullName, relationship.dependentTable))).filter(Boolean);

        const uniqueTables = new Map([[selectedTable.fullName, selectedTable]]);
        for (const table of principalTables) {
            uniqueTables.set(table.fullName, table);
        }
        for (const table of dependentTables) {
            uniqueTables.set(table.fullName, table);
        }

        const relatedRelationships = allRelationships.filter((relationship) =>
            uniqueTables.has(relationship.principalTable) &&
            uniqueTables.has(relationship.dependentTable) &&
            (sameText(relationship.principalTable, tableFullName) || sameText(relationship.dependentTable, tableFullName)));

        const positions = new Map([[selectedTable.fullName, { x: 460, y: 220 }]]);
        const principalLayout = createMasonryLayout(Array.from(new Map(principalTables.map((table) => [table.fullName, table])).values()), 42, 120, 1);
        const dependentLayout = createMasonryLayout(Array.from(new Map(dependentTables.map((table) => [table.fullName, table])).values()), 886, 120, 1);
        for (const entry of principalLayout.positions.entries()) {
            positions.set(entry[0], entry[1]);
        }
        for (const entry of dependentLayout.positions.entries()) {
            positions.set(entry[0], entry[1]);
        }

        return createGraph(Array.from(uniqueTables.values()), relatedRelationships, positions, selectedTable.fullName);
    }

    function buildDomainOptions() {
        const groups = new Map();
        for (const table of allTables) {
            const key = table.domainName || 'Outros';
            groups.set(key, (groups.get(key) || 0) + 1);
        }

        const options = Array.from(groups.entries())
            .sort((left, right) => compareText(left[0], right[0]))
            .map(([value, count]) => ({ value: value, count: count }));
        return [{ value: ALL_DOMAIN, count: allTables.length }].concat(options);
    }

    const domainOptions = buildDomainOptions();

    function renderFieldFlags(column) {
        const flags = [];
        if (column.isPrimaryKey) {
            flags.push(e('span', { className: 'er-table-node__flag er-table-node__flag-pk', key: 'pk' }, 'PK'));
        }
        if (column.isForeignKey) {
            flags.push(e('span', { className: 'er-table-node__flag er-table-node__flag-fk', key: 'fk' }, 'FK'));
        }
        if (column.isNullable) {
            flags.push(e('span', { className: 'er-table-node__flag er-table-node__flag-null', key: 'null' }, 'NULL'));
        }

        return flags;
    }

    function TableNode(props) {
        const data = props.data || {};
        const columns = Array.isArray(data.columns) ? data.columns : [];
        const rows = columns.length === 0
            ? e('div', { className: 'er-table-node__empty' }, 'Sem colunas mapeadas')
            : columns.map((column, index) => e('div', { className: 'er-table-node__row', key: `${data.fullName}-column-${index}` }, e('div', { className: 'er-table-node__field' }, ...renderFieldFlags(column), e('span', { className: 'er-table-node__field-name' }, column.name || '-')), e('span', { className: 'er-table-node__field-type' }, column.storeType || 'desconhecido')));

        return e('div', { className: data.focused ? 'er-table-node er-table-node-focused' : 'er-table-node' }, e('div', { className: 'er-table-node__header', style: { background: data.domainColor || '#334155' } }, e('div', { className: 'er-table-node__title' }, e('span', null, data.name || data.fullName || 'Tabela'), e('span', { className: 'er-table-node__schema' }, data.schema || 'dbo')), e('div', { className: 'er-table-node__subtitle' }, e('span', { className: 'er-table-node__domain' }, data.domainName || 'Outros'), e('span', null, `${columns.length} coluna(s)`))), e('div', { className: 'er-table-node__body' }, rows), e(Handle, { type: 'target', position: Position.Left }), e(Handle, { type: 'source', position: Position.Right }));
    }

    const nodeTypes = { table: TableNode };

    function renderDomainChip(option, selectedDomain, setSelectedDomain, setFocusedTable) {
        return e('button', {
            type: 'button',
            key: option.value,
            className: option.value === selectedDomain ? 'er-domain-chip er-domain-chip-active' : 'er-domain-chip',
            onClick: function () {
                setSelectedDomain(option.value);
                setFocusedTable('');
            }
        }, option.value === ALL_DOMAIN ? `Todos (${option.count})` : `${option.value} (${option.count})`);
    }

    function renderTableButton(table, focusedTable, setFocusedTable) {
        return e('button', {
            type: 'button',
            key: table.fullName,
            className: sameText(focusedTable, table.fullName) ? 'er-table-button er-table-button-active' : 'er-table-button',
            onClick: function () { setFocusedTable(table.fullName); }
        }, e('div', { className: 'er-table-button__name' }, table.fullName), e('div', { className: 'er-table-button__meta' }, e('span', null, `${table.totalColumns} coluna(s)`), e('span', null, `Schema: ${table.schema}`)));
    }

    function ErDiagramPage() {
        const [selectedDomain, setSelectedDomain] = useState(ALL_DOMAIN);
        const [focusedTable, setFocusedTable] = useState('');
        const [flowInstance, setFlowInstance] = useState(null);
        const [layoutNonce, setLayoutNonce] = useState(0);

        const focused = Boolean(focusedTable);
        const baseGraph = focused ? buildFocusGraph(focusedTable) : selectedDomain === ALL_DOMAIN ? buildOverviewGraph() : buildDomainGraph(selectedDomain);
        const autoLayoutDirection = resolveAutoLayoutDirection(baseGraph.nodes.length, focused);
        const graph = canAutoLayout ? createAutoLayoutGraph(baseGraph, autoLayoutDirection) : baseGraph;
        const visibleTables = selectedDomain === ALL_DOMAIN ? allTables : allTables.filter((table) => sameText(table.domainName, selectedDomain));
        const groupedTables = new Map();

        for (const table of [...visibleTables].sort((left, right) => compareText(left.fullName, right.fullName))) {
            const key = table.domainName || 'Outros';
            if (!groupedTables.has(key)) {
                groupedTables.set(key, []);
            }

            groupedTables.get(key).push(table);
        }

        useEffect(function () {
            if (!flowInstance || graph.nodes.length === 0) {
                return;
            }

            const timerId = window.setTimeout(function () {
                flowInstance.fitView({
                    padding: focused ? 0.22 : 0.16,
                    duration: 280,
                    maxZoom: focused ? 1.15 : 0.95
                });
            }, 40);

            return function () { window.clearTimeout(timerId); };
        }, [flowInstance, selectedDomain, focusedTable, focused, graph.nodes.length, graph.edges.length, layoutNonce]);

        const currentScopeLabel = selectedDomain === ALL_DOMAIN ? 'Todos os dominios' : selectedDomain;
        const groupedSections = Array.from(groupedTables.entries())
            .sort((left, right) => compareText(left[0], right[0]))
            .map(function (entry) {
                return e(
                    'section',
                    { key: entry[0] },
                    e(
                        'div',
                        { className: 'er-sidebar-group__title' },
                        e('strong', null, entry[0]),
                        e('span', null, `${entry[1].length} tabela(s)`)
                    ),
                    e(
                        'div',
                        { className: 'er-table-list' },
                        ...entry[1].map((table) => renderTableButton(table, focusedTable, setFocusedTable))
                    )
                );
            });

        const canvasContent = graph.nodes.length === 0
            ? e('div', { className: 'er-flow-empty' }, 'Nenhuma tabela disponivel para o recorte selecionado.')
            : e(
                ReactFlow,
                {
                    className: 'er-reactflow',
                    nodes: graph.nodes,
                    edges: graph.edges,
                    nodeTypes: nodeTypes,
                    onInit: setFlowInstance,
                    onNodeClick: function (_, node) { setFocusedTable(node.id); },
                    fitView: true,
                    fitViewOptions: { padding: 0.16, maxZoom: 1.05 },
                    minZoom: 0.12,
                    maxZoom: 1.8,
                    nodesConnectable: false,
                    elementsSelectable: true,
                    attributionPosition: 'bottom-left',
                    defaultEdgeOptions: { type: 'smoothstep' }
                },
                e(Background, { color: '#dbe4f0', gap: 22 }),
                e(MiniMap, {
                    pannable: true,
                    zoomable: true,
                    nodeColor: function (node) { return node && node.data ? node.data.domainColor : '#334155'; },
                    maskColor: 'rgba(15, 23, 42, .08)'
                }),
                e(Controls, { showInteractive: false })
            );

        const sidebar = e(
            'aside',
            { className: 'er-sidebar' },
            e(
                'section',
                { className: 'er-sidebar-card' },
                e(
                    'div',
                    { className: 'er-sidebar-card__header' },
                    e('div', { className: 'small text-uppercase fw-bold text-primary mb-1' }, 'Recorte'),
                    e('h5', { className: 'h6 mb-1' }, 'Dominios e contextos'),
                    e('div', { className: 'text-muted small mb-0' }, 'Selecione um recorte para reduzir ruido e inspecionar relacoes do contexto desejado.')
                ),
                e(
                    'div',
                    { className: 'er-sidebar-card__body' },
                    e(
                        'div',
                        { className: 'er-domain-chip-list' },
                        ...domainOptions.map((option) => renderDomainChip(option, selectedDomain, setSelectedDomain, setFocusedTable))
                    ),
                    e(
                        'div',
                        { className: 'er-kpi-grid' },
                        e('div', { className: 'er-kpi' }, e('div', { className: 'er-kpi__label' }, 'Recorte'), e('div', { className: 'er-kpi__value' }, currentScopeLabel)),
                        e('div', { className: 'er-kpi' }, e('div', { className: 'er-kpi__label' }, 'Tabelas'), e('div', { className: 'er-kpi__value' }, graph.nodes.length)),
                        e('div', { className: 'er-kpi' }, e('div', { className: 'er-kpi__label' }, 'FKs'), e('div', { className: 'er-kpi__value' }, graph.edges.length))
                    )
                )
            ),
            e(
                'section',
                { className: 'er-sidebar-card' },
                e(
                    'div',
                    { className: 'er-sidebar-card__header' },
                    e('div', { className: 'small text-uppercase fw-bold text-primary mb-1' }, 'Inventario'),
                    e('h5', { className: 'h6 mb-1' }, 'Tabelas do recorte'),
                    e('div', { className: 'text-muted small mb-0' }, 'Clique em uma tabela para abrir foco local com dependencias diretas.')
                ),
                e(
                    'div',
                    { className: 'er-sidebar-card__body' },
                    e(
                        'div',
                        { className: 'er-sidebar-actions mb-3' },
                        e('button', { type: 'button', className: 'btn btn-sm btn-outline-secondary', onClick: function () { setFocusedTable(''); } }, 'Limpar foco'),
                        e('button', { type: 'button', className: 'btn btn-sm btn-outline-primary', onClick: function () { setSelectedDomain(ALL_DOMAIN); setFocusedTable(''); } }, 'Ver todos os dominios')
                    ),
                    e('div', { className: 'er-sidebar-groups' }, ...groupedSections)
                )
            )
        );

        return e(
            'div',
            { className: 'er-react-app' },
            sidebar,
            e(
                'section',
                { className: 'er-canvas-card' },
                e(
                    'div',
                    { className: 'er-canvas-card__header' },
                    e(
                        'div',
                        { className: 'er-toolbar' },
                        e(
                            'div',
                            null,
                            e('div', { className: 'small text-uppercase fw-bold text-primary mb-1' }, 'Canvas ER'),
                            e('h5', { className: 'h6 mb-1' }, focused ? `Foco em ${focusedTable}` : `Recorte ${currentScopeLabel}`),
                            e('div', { className: 'text-muted small mb-0' }, 'Arraste os cards, use o minimapa e os controles nativos do ReactFlow para navegar no grafo.')
                        ),
                        e(
                            'div',
                            { className: 'er-toolbar__summary' },
                            e('span', { className: 'er-toolbar__badge' }, `${graph.nodes.length} tabela(s)`),
                            e('span', { className: 'er-toolbar__badge' }, `${graph.edges.length} relacionamento(s)`),
                            e('span', { className: 'er-toolbar__badge' }, focused ? 'Modo foco' : 'Modo recorte')
                        ),
                        e(
                            'div',
                            { className: 'er-toolbar__actions' },
                            e(
                                'button',
                                {
                                    type: 'button',
                                    className: 'btn btn-sm btn-primary',
                                    disabled: !canAutoLayout,
                                    onClick: function () { setLayoutNonce((value) => value + 1); },
                                    title: canAutoLayout
                                        ? 'Reorganizar o grafo atual automaticamente'
                                        : 'Auto-layout indisponivel'
                                },
                                canAutoLayout ? 'Reaplicar auto-layout' : 'Auto-layout indisponivel'
                            )
                        )
                    )
                ),
                e(
                    'div',
                    { className: 'er-canvas-card__body' },
                    e('div', { className: 'er-canvas-stage' }, canvasContent)
                )
            )
        );
    }

    ReactDOM.createRoot(host).render(e(ErDiagramPage));
})();

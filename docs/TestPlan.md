# 基于数字孪生的多AGV协同调度管理与仿真系统 - 测试计划

## 1. 测试范围

### 1.1 测试系统

| 序号 | 测试系统 | 版本 |
|------|----------|------|
| 1 | 基于数字孪生的多AGV协同调度管理与仿真系统 (RAWSimO WebServer) | v1.0 |
| 2 | 多场景通用AGV协同调度管控云平台 (mir_fe) | v1.0 |

### 1.2 测试环境要求

- 操作系统: Linux (Docker 部署)
- 后端: .NET 10.0, ASP.NET Core
- 前端: Vue.js 2.x + Element UI
- 测试工具: 浏览器 (Chrome/Edge), curl, Postman

---

## 2. 测试实例与配置

### 2.1 文件清单

**公共配置** (`default_sim_config/`):

| 文件 | 说明 |
|------|------|
| `1-3-3-15-64.xinst` | 基础实例：1层、15台AGV、3补货站、3拣选站、64料架 |
| `steadysetting.xsett` | 稳态仿真设置，OrderCount=200，SimulationDuration=1000000 |
| `HardwareTestFAR.xconf` | FAR路径规划控制器配置 |
| `Resources.zip` | 仿真资源文件 |

**测试专用配置** (`test_sim_config/`):

| 文件 | 说明 |
|------|------|
| `Repo300AGV.xlayo` | 300台AGV布局文件（HighwayHallway aisle，4补货站+4拣选站） |
| `TestScale-300AGV-5000Task.xsett` | 300AGV+5000任务综合测试设置，OrderCount=5000 |
| `TestScale-3000Task.xsett` | 3000+任务专用设置（配合基础实例），OrderCount=5000 |
| `SimpleItem-Fill-200-200-172800.xsett` | 200x200 SKU长时仿真设置 |

### 2.2 测试配置组合

| 配置名 | Instance | Setting | Control | 说明 |
|--------|----------|---------|---------|------|
| Basic | `1-3-3-15-64.xinst` | `steadysetting.xsett` | `HardwareTestFAR.xconf` | 15台AGV基础功能测试 |
| Scale-200+ | `Repo300AGV.xlayo` | `TestScale-300AGV-5000Task.xsett` | `HardwareTestFAR.xconf` | 300台AGV + 5000任务综合测试 |
| Scale-3000+ | `1-3-3-15-64.xinst` | `TestScale-3000Task.xsett` | `HardwareTestFAR.xconf` | 15台AGV + 5000任务（验证任务规模） |

### 2.3 性能测试参数

- AGV数量: >= 200 台
- 待调度任务数 (Pending): >= 3000 个
- 目标平均任务响应时间: < 3 秒

---

## 3. 测试用例

### 3.1 AGV管控系统测试

#### TC-01: 概要面板

- **检测项目**: 系统支持显示 AGV 数量、任务数量、已接入站点数量、告警状态
- **前置条件**: 系统正常运行，仿真已启动
- **测试步骤**:
  1. 启动仿真（使用 Basic 配置）
  2. 进入仿真可视化页面
  3. 检查概要面板区域
  4. 验证显示的AGV数量与实例配置一致（15台）
  5. 验证 Queue Status 显示 Total Orders、Pending、Open、Completed
  6. 验证显示的站点数量（3补货站 + 3拣选站）
  7. 验证告警状态信息（Health: OK）
- **预期结果**: 概要面板正确显示 AGV 总数、任务统计（累计总数/待分配/执行中/已完成）、站点数量、告警状态
- **判定标准**: 所有数据项均正确显示且实时更新

#### TC-02: 实时监控系统

- **检测项目**: 系统支持显示已接入AGV的实时位置
- **前置条件**: 仿真运行中
- **测试步骤**:
  1. 在可视化页面观察AGV分布（红色圆点）
  2. 等待若干仿真步后再次观察
  3. 验证AGV位置在渲染图中实时更新
- **预期结果**: 渲染图中红色圆点（AGV）位置随仿真推进实时变化，每个AGV显示朝向指示线
- **判定标准**: AGV位置通过 SSE 流每100ms更新，无明显延迟

#### TC-03: 车辆数据统计

- **检测项目**: 系统支持显示AGV状态的统计信息
- **前置条件**: 仿真运行中
- **测试步骤**:
  1. 查看仿真页面 Status 区域的 Idle/Busy 统计
  2. 通过 `GetTestMetadata` API 获取 `idleBotCount` / `busyBotCount` 验证
  3. 验证 idleBotCount + busyBotCount = agvCount
- **预期结果**: 系统正确显示各状态AGV的统计数据
- **判定标准**: 统计数据与实际AGV状态一致，总数吻合

#### TC-04: 任务数据统计

- **检测项目**: 系统支持显示AGV执行过的任务的统计信息
- **前置条件**: 仿真运行中且有已完成的任务
- **测试步骤**:
  1. 等待仿真运行产生已完成任务
  2. 查看 Queue Status 区域的 Pending / Open / Completed / Total Orders
  3. 验证 Pending 为当前待调度任务池大小，Completed 持续增长
  4. 通过 SSE 流和 `GetTestMetadata` API 交叉验证
- **预期结果**: 系统正确显示 Total Orders（累计总任务数）、Pending（待调度）、Open（执行中）、Completed（已完成）
- **判定标准**: Pending 由 OrderCount 配置控制，Completed 持续增长，Total Orders = 三者之和

#### TC-05: 新增AGV

- **检测项目**: 系统支持新增AGV
- **前置条件**: 仿真未运行，实例已加载
- **测试步骤**:
  1. 加载实例配置文件
  2. 在 XML 编辑器中增加 Bot 节点
  3. 启动仿真
  4. 验证新增的AGV出现在渲染图和统计数据中
- **预期结果**: 系统可识别新增的AGV并在渲染和统计中正确体现
- **判定标准**: AGV数量与修改后的配置一致

#### TC-06: 新增AGV数量 (200+)

- **检测项目**: 系统支持接入至少200台AGV
- **前置条件**: Scale-200+ 配置已准备
- **测试步骤**:
  1. 进入 Test Mode，点击 "Load >200AGV & >3000Task"
  2. 系统自动加载 `Repo300AGV.xlayo` + `TestScale-300AGV-5000Task.xsett`
  3. 启动仿真
  4. 验证 AGV Count >= 200（实际300台）
  5. 检查渲染图正常显示所有AGV
  6. 通过 `GetTestMetadata` API 确认 agvCount
- **预期结果**: 系统成功接入300台AGV，所有AGV状态正常
- **判定标准**: AGV接入数量 >= 200，无错误

#### TC-07: 修改AGV属性

- **检测项目**: 系统支持修改已接入AGV属性
- **前置条件**: 实例已加载
- **测试步骤**:
  1. 在实例配置 XML 中修改 Bot 属性（如 MaxVelocity、MaxAcceleration）
  2. 启动仿真
  3. 验证AGV按修改后的属性运行
- **预期结果**: AGV属性修改后仿真按新参数运行
- **判定标准**: 修改后的属性值在运行中得到体现

#### TC-08: 用户管理

- **检测项目**: 系统支持添加/修改用户信息
- **前置条件**: 系统部署完成
- **测试步骤**:
  1. 通过前端用户管理页面添加新用户
  2. 使用新用户登录
  3. 修改用户信息
  4. 验证修改后的信息生效
- **预期结果**: 用户管理功能正常，增删改查操作均可用
- **判定标准**: 用户数据持久化，权限正确

#### TC-09: 站点管理

- **检测项目**: 系统支持管理已接入站点
- **前置条件**: 仿真已启动
- **测试步骤**:
  1. 查看 SSE 数据中的 InputStations / OutputStations 列表
  2. 通过 `GetTestMetadata` API 获取 inputStationCount / outputStationCount
  3. 验证站点数量与实例配置一致
- **预期结果**: 系统正确显示和管理所有站点
- **判定标准**: 站点数据与实例配置一致

#### TC-10: 标记管理

- **检测项目**: 系统支持管理已接入站点的地图标记
- **前置条件**: 仿真已启动
- **测试步骤**:
  1. 勾选 Waypoints 显示选项
  2. 验证渲染图中显示所有路径节点（灰色小点）
  3. 通过 `GetTestMetadata` API 获取 waypointCount 验证数量
- **预期结果**: 地图标记（Waypoints）正确显示在渲染图中
- **判定标准**: 节点位置精确，数量与路径规划一致

#### TC-11: AGV状态

- **检测项目**: 系统支持实时查看已接入AGV状态
- **前置条件**: 仿真运行中
- **测试步骤**:
  1. 通过 SSE 流获取实时数据（每个Bot的 x, y, orientation, state）
  2. 通过 `GetTestMetadata` API 获取 botStates 列表
  3. 验证每个AGV包含 state（Rest/Move/PickupPod等）、isIdle、hasPod
  4. 暂停/恢复仿真验证状态变化
- **预期结果**: 每个AGV的状态实时更新
- **判定标准**: 状态数据包含位置(x,y)、朝向(orientation)、状态(state)、是否载Pod(hasPod)

#### TC-12: AGV健康

- **检测项目**: 系统支持实时查看已接入AGV健康信息
- **前置条件**: 仿真运行中
- **测试步骤**:
  1. 调用 `GET /simulation/Health` 端点，验证返回 true
  2. 通过 `GetTestMetadata` API 检查 healthOk 字段
  3. 验证 error 字段为空
  4. 检查仿真日志中无异常
- **预期结果**: 系统健康检查端点返回正常，无AGV异常
- **判定标准**: Health 端点返回 true，healthOk = true，error 为 null

#### TC-13: 任务列表

- **检测项目**: 系统支持查看已配置任务列表
- **前置条件**: 实例已加载
- **测试步骤**:
  1. 加载实例后调用 `GetSimulationStatus` 查看可用物品描述列表（availableItemDescriptions）
  2. 启动仿真
  3. 通过 SSE 流查看 Total Orders 增长
  4. 使用 Append Tasks 对话框验证物品列表完整
- **预期结果**: 系统显示所有配置的任务及其详情
- **判定标准**: 物品描述数据完整，可据此创建任务

#### TC-14: 实时任务

- **检测项目**: 系统支持查看/派发/中断实时AGV任务及状态
- **前置条件**: 仿真运行中
- **测试步骤**:
  1. **查看**: 通过 SSE 流查看当前任务状态（Total Orders / Pending / Open / Completed）
  2. **派发**: 使用 Append Tasks 功能派发新任务
     - 选择目标输出站
     - 选择物品和数量
     - 提交任务，验证 Total Orders 增长
  3. **中断**: 使用 Pause 功能暂停仿真
     - 验证任务执行暂停（SimTime 不再增长）
     - 使用 Resume 恢复
  4. **停止**: 使用 Stop 功能停止仿真
- **预期结果**: 查看实时任务状态、派发新任务、暂停/恢复/停止功能均正常
- **判定标准**: 各操作响应正确，Total Orders 和各子项状态实时更新

### 3.2 调度系统测试

#### TC-15: 任务调度

- **检测项目**: 系统支持加载自定义任务信息并进行调度优化
- **前置条件**: 系统运行正常
- **测试步骤**:
  1. 加载实例配置（含自定义任务信息）
  2. 选择调度控制器配置（当前使用 FAR 路径规划）
  3. 启动仿真
  4. 观察任务调度过程（Open Orders 增长说明任务正在分配执行）
  5. 动态追加自定义任务 (Append Tasks)
- **预期结果**: 系统按配置的调度策略分配任务给AGV
- **判定标准**: 任务被正确分配和执行，AGV按调度指令移动

#### TC-16: 调度任务规模 (3000+ 任务)

- **检测项目**: 待调度任务不少于 3000 个的条件下，系统平均任务响应时间小于 3 秒
- **前置条件**: Scale-3000+ 配置已准备
- **测试步骤**:
  1. 进入 Test Mode，点击 "Load >3000Task" 加载基础实例 + 高任务设置
  2. 启动仿真
  3. 等待仿真自动填充任务池（OrderCount=5000，Fill模式）
  4. 验证 Pending Order Count >= 3000
  5. 通过 Append Tasks 追加批量任务，记录每次 API 响应时间
  6. 查看 Test Mode 面板中的 Avg Append Time 指标
- **预期结果**: Pending Orders >= 3000，平均任务响应时间 < 3 秒
- **判定标准**: Pending >= 3000 条件下，AppendTasks API 平均响应时间 < 3000ms

#### TC-17: 调度可视化

- **检测项目**: 系统支持显示调度的结果回放
- **前置条件**: 仿真运行中或已完成
- **测试步骤**:
  1. 启动仿真并运行一段时间
  2. 观察 ECharts 渲染图中的实时调度过程（AGV移动、Pod搬运）
  3. 验证 AGV 移动轨迹和任务分配可视化
  4. 暂停仿真查看当前状态快照
  5. 停止仿真后点击 Download Data 下载统计数据 (ZIP)
  6. 验证统计文件包含完整调度结果
- **预期结果**: 调度过程实时可视化，统计数据可下载
- **判定标准**: 渲染图实时更新，统计ZIP包含完整调度数据

### 3.3 仿真系统测试

#### TC-18: 仿真可视化

- **检测项目**: 系统支持显示仿真系统的可视化界面
- **前置条件**: 仿真已启动
- **测试步骤**:
  1. 启动仿真
  2. 验证可视化界面正常显示
  3. 检查各元素渲染: Bots(红色圆)、Pods(橙色圆)、Stations(蓝/绿矩形)、Waypoints(灰点)
  4. 调整 Render Options（切换 Bots/Pods/Stations/Waypoints 显示，验证即时生效）
  5. 调整 TierIndex（多层布局时切换层级）
- **预期结果**: 可视化界面正常显示所有元素，渲染选项可切换
- **判定标准**: 所有渲染元素正确显示，切换无异常

### 3.4 日志系统测试

#### TC-19: 告警记录管理

- **检测项目**: 系统支持查看车辆的告警记录
- **前置条件**: 仿真已运行
- **测试步骤**:
  1. 启动仿真
  2. 通过 `GetTestMetadata` API 检查 healthOk 和 error 字段
  3. 下载仿真统计数据，检查日志文件内容
  4. 检查前端告警记录页面
- **预期结果**: 告警记录可查看，包含时间戳和告警详情
- **判定标准**: 正常运行时无告警，异常时告警信息完整

#### TC-20: 日志审计

- **检测项目**: 系统支持查看API端点日志
- **前置条件**: 系统运行中
- **测试步骤**:
  1. 依次调用各 API 端点 (Health, GetStatus, Start, Stop, Pause, Resume, AppendTasks, GetTestMetadata)
  2. 查看服务器端控制台日志输出
  3. 验证下载的统计数据中包含日志文件
- **预期结果**: 所有 API 调用均有日志记录
- **判定标准**: 日志记录完整，包含请求时间、端点、响应状态

---

## 4. 测试模式

系统提供 **测试模式** 功能，在仿真可视化页面通过 "Test Mode" 按钮进入。

### 4.1 测试模式功能

- 一键加载测试配置（Basic / Scale-200+ / Scale-3000+）
- 20项测试用例表格，实时显示 PASS/FAIL/RUNNING/PENDING 状态
- 实时测试指标面板（AGV数量、任务统计、健康状态、响应时间等）
- 生成测试报告（下载为文本文件）

### 4.2 测试模式数据指标

| 指标 | 数据来源 | 对应测试项 |
|------|----------|-----------|
| AGV Count | SSE流 / GetTestMetadata | TC-01, TC-06 |
| Total Orders | SSE流 totalOrderCount | TC-04, TC-16 |
| Pending / Open / Completed | SSE流 | TC-01, TC-04, TC-14 |
| Station Count (in/out) | GetTestMetadata | TC-01, TC-09 |
| Bot States (idle/busy) | GetTestMetadata botStates | TC-03, TC-11 |
| Health Status | GetTestMetadata healthOk | TC-12, TC-19 |
| Avg Append Time | 前端计时 appendTimingHistory | TC-16 |
| Pod Count | GetTestMetadata | TC-01 |
| Waypoint Count | GetTestMetadata | TC-10 |

### 4.3 测试模式自动判定逻辑

| 测试项 | PASS条件 |
|--------|----------|
| TC-06 (200+ AGV) | agvCount >= 200 |
| TC-12 (健康) | healthOk = true |
| TC-16 (响应时间) | appendTimingHistory 平均值 < 3000ms |

---

## 5. 性能测试方案

### 5.1 AGV规模测试

```text
配置: Scale-200+ (Repo300AGV.xlayo + TestScale-300AGV-5000Task.xsett + HardwareTestFAR.xconf)
一键加载: Test Mode → "Load >200AGV & >3000Task"
验证: agvCount >= 200（实际300台），仿真正常运行无错误
后端测试: PerformanceTests.Scale200AGV_BotCountExceeds200
         PerformanceTests.Scale200AGV_SimulationCompletes
```

### 5.2 任务规模测试

```text
配置: Scale-3000+ (1-3-3-15-64.xinst + TestScale-3000Task.xsett + HardwareTestFAR.xconf)
一键加载: Test Mode → "Load >3000Task"
验证: Pending Order Count >= 3000，Fill模式自动填充OrderCount=5000的任务池
后端测试: PerformanceTests.Scale3000Task_OrderCountExceeds3000
         PerformanceTests.Scale3000Task_OrderGenerationPerformance
```

### 5.3 综合规模测试 (>200 AGV + >3000 任务)

```text
配置: Scale-200+ (Repo300AGV.xlayo + TestScale-300AGV-5000Task.xsett + HardwareTestFAR.xconf)
一键加载: Test Mode → "Load >200AGV & >3000Task"
验证:
  - AGV 接入数量 >= 200 (实际300台)
  - Pending Orders >= 3000 (OrderCount=5000, Fill模式填充任务池)
  - AppendTasks API 平均响应时间 < 3秒
判定: 三项均通过则综合判定 PASS
```

---

## 6. 后端自动化测试

### 6.1 测试项目

测试位于 `Tests/RAWSimO.Core.Tests/PerformanceTests.cs`，使用 xUnit 框架。

### 6.2 测试资源

| 文件 | 说明 |
|------|------|
| `Resources/BasicInstance.xlayo` | 16台AGV基础布局 |
| `Resources/BasicInstance.xsett` | 基础设置，Duration=600，OrderCount=200 |
| `Resources/BasicInstance.xconf` | FAR + Balanced调度控制配置 |
| `Resources/Scale200AGV.xlayo` | 300台AGV布局 |
| `Resources/Scale200AGV.xsett` | 200+AGV短时测试设置，Duration=100 |
| `Resources/Scale3000Task.xsett` | 3000+任务设置，OrderCount=5000，Duration=100 |

### 6.3 测试用例

| 测试方法 | 说明 | 验证条件 |
|----------|------|----------|
| `Scale200AGV_BotCountExceeds200` | 300台AGV实例加载验证 | botCount >= 200 |
| `Scale200AGV_SimulationCompletes` | 300台AGV仿真完整运行 | 无异常，SimTime >= Duration |
| `Scale3000Task_OrderCountExceeds3000` | 高任务量订单生成验证 | totalOrders >= 3000 |
| `Scale3000Task_OrderGenerationPerformance` | 高任务量性能验证 | 执行时间 < 30000ms |
| `Dashboard_AllMetricsAccessible` | 仪表盘数据可达性 | Bot/Station/Pod/Waypoint > 0 |
| `Health_SimulationNoError` | 仿真健康检查 | 完整运行无异常 |

### 6.4 运行方式

```bash
dotnet test Tests/RAWSimO.Core.Tests/RAWSimO.Core.Tests.csproj --filter "PerformanceTests"
```

---

## 7. 测试结果判定标准

| 等级 | 说明 |
|------|------|
| PASS | 测试结果完全符合预期 |
| PASS(W) | 测试结果基本符合，存在轻微问题但不影响功能 |
| FAIL | 测试结果不符合预期，需修复后重测 |
| N/A | 该测试项不适用于当前测试环境 |

---

## 8. 附录

### 8.1 API端点列表

| 端点 | 方法 | 说明 |
|------|------|------|
| `/simulation/Health` | GET | 健康检查 |
| `/simulation/GetSimulationStatus` | GET | 获取仿真状态（含可用物品描述） |
| `/simulation/StartSimulation` | POST | 启动仿真 |
| `/simulation/StopSimulation` | POST | 停止仿真 |
| `/simulation/PauseSimulation` | POST | 暂停仿真 |
| `/simulation/ResumeSimulation` | POST | 恢复仿真 |
| `/simulation/GetLatestFrame` | POST | 获取最新渲染帧 |
| `/simulation/AppendTasks` | POST | 追加任务 |
| `/simulation/UpdateRenderOptions` | POST | 更新渲染选项 |
| `/simulation/DownloadStatistics` | GET | 下载统计数据 (ZIP) |
| `/simulation/StreamFrames` | GET(SSE) | 实时帧流 |
| `/simulation/StreamSimulationData` | GET(SSE) | 实时仿真数据流（含 totalOrderCount） |
| `/simulation/GetTestMetadata` | GET | 获取测试元数据（AGV统计、Bot状态、健康等） |

### 8.2 SSE数据字段

`StreamSimulationData` 返回的 JSON 字段:

| 字段 | 类型 | 说明 |
|------|------|------|
| `simTime` | double | 当前仿真时间 |
| `worldWidth` / `worldHeight` | double | 仿真世界尺寸 |
| `pendingOrderCount` | int | 待分配任务数 |
| `openOrderCount` | int | 执行中任务数 |
| `completedOrderCount` | int | 已完成任务数 |
| `totalOrderCount` | int | 累计总任务数 |
| `bots` | array | AGV列表（id, x, y, radius, orientation） |
| `pods` | array | 料架列表（id, x, y, radius, contents） |
| `inputStations` | array | 补货站列表 |
| `outputStations` | array | 拣选站列表 |
| `waypoints` | array | 路径节点列表 |
